// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Services.Configuration;

namespace CopyAzureContainer
{
    class CopyAzureContainerJob : JsonConfigurationJob
    {
        private const string SectionName = "CopyAzureContainer";
        private const string DestinationLogsContainerName = "logs";
        private const string AzCopyPath = @"tools\azcopy\azCopy.exe";
        private readonly int DefaultBackupDays = -1;
        private string _managedIdentityClientId;
        private bool _storageUseManagedIdentity;
        private string _destStorageAccountName;
        private string _destStorageSasValue;
        private int _backupDays;

        private IEnumerable<AzureContainerInfo> _sourceContainers;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            var jobConfiguration = _serviceProvider.GetRequiredService<IOptionsSnapshot<CopyAzureContainerConfiguration>>().Value;

            _backupDays = jobConfiguration.BackupDays ?? DefaultBackupDays;
            _destStorageAccountName = jobConfiguration.DestStorageAccountName ?? throw new InvalidOperationException(nameof(jobConfiguration.DestStorageAccountName) + " is required.");
            _destStorageSasValue = jobConfiguration.DestStorageSasValue;

            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            _managedIdentityClientId = configuration.GetValue<string>(Constants.ManagedIdentityClientIdKey);
            _storageUseManagedIdentity = configuration.GetValue<bool>(Constants.StorageUseManagedIdentityPropertyName);
            if (!_storageUseManagedIdentity && string.IsNullOrEmpty(_destStorageSasValue))
            {
                throw new ArgumentException($"One of {nameof(_storageUseManagedIdentity)} or {nameof(jobConfiguration.DestStorageSasValue)} should be defined.");
            }

#if !DEBUG
            if (_storageUseManagedIdentity)
            {
                Environment.SetEnvironmentVariable("AZCOPY_AUTO_LOGIN_TYPE", "MSI");
                Environment.SetEnvironmentVariable("AZCOPY_MSI_CLIENT_ID", _managedIdentityClientId);
            }
#endif
            _sourceContainers = jobConfiguration.SourceContainers ?? throw new InvalidOperationException(nameof(jobConfiguration.SourceContainers) + " is required.");
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<CopyAzureContainerConfiguration>(configurationRoot.GetSection(SectionName));
        }

        public override async Task Run()
        {
            var currentDate = DateTimeOffset.UtcNow;

            if (await TryCreateDestinationContainerAsync(DestinationLogsContainerName))
            {
                foreach (AzureContainerInfo sourceContainer in _sourceContainers)
                {
                    if (_backupDays > 0)
                    {
                        await TryDeleteDestinationContainerAsync(sourceContainer.ContainerName);
                    }

                    await TryCopyContainerAsync(sourceContainer, currentDate);
                }
            }
        }

        private async Task<bool> TryCopyContainerAsync(AzureContainerInfo containerInfo, DateTimeOffset date)
        {
            var sw = new Stopwatch();
            var azCopyTempFolder = $@"{Directory.GetCurrentDirectory()}\azCopy_{containerInfo.ContainerName}";
            Environment.SetEnvironmentVariable("AZCOPY_JOB_PLAN_LOCATION", azCopyTempFolder);
            Environment.SetEnvironmentVariable("AZCOPY_LOG_LOCATION", azCopyTempFolder);
            var destContainer = FormatDestinationContainerName(date, containerInfo.ContainerName);
            RefreshLogData(azCopyTempFolder);

            if (await TryCreateDestinationContainerAsync(destContainer))
            {
                var sourceUrl = $"https://{containerInfo.StorageAccountName}.blob.core.windows.net/{containerInfo.ContainerName}";
                var destinationUrl = $"https://{_destStorageAccountName}.blob.core.windows.net/{destContainer}";

                var logsSourceUrl = $"{azCopyTempFolder}/*.log";
                var logsDestinationUrl = $"https://{_destStorageAccountName}.blob.core.windows.net/logs/{destContainer}";

                if (!string.IsNullOrEmpty(containerInfo.StorageSasToken))
                {
                    sourceUrl += $"?" + containerInfo.StorageSasToken.TrimStart('?');
                }

                if (!string.IsNullOrEmpty(_destStorageSasValue))
                {
                    destinationUrl += $"?" + _destStorageSasValue.TrimStart('?');
                    logsDestinationUrl += $"?" + _destStorageSasValue.TrimStart('?');
                }

                var arguments = $"copy {sourceUrl} {destinationUrl} --recursive --log-level WARNING";
                var argumentsLog = $"copy {logsSourceUrl} {logsDestinationUrl} --recursive --as-subdir=false";

#if DEBUG
                // When testing locally if you don't provide sas tokens for authentication we need to perform the "azcopy login" command.
                // This command will display a link and a code in the terminal to authorize your device to perform the following commands.
                // See: https://learn.microsoft.com/azure/storage/common/storage-use-azcopy-authorize-azure-active-directory#authorize-a-user-identity-azcopy-login-command
                if (string.IsNullOrEmpty(_destStorageSasValue) || string.IsNullOrEmpty(containerInfo.StorageSasToken))
                {
                    try
                    {
                        ProcessStartInfo copyToAzureProc = new ProcessStartInfo();
                        copyToAzureProc.FileName = $"{AzCopyPath}";
                        copyToAzureProc.Arguments = $"login";
                        copyToAzureProc.UseShellExecute = false;

                        using (var p = Process.Start(copyToAzureProc))
                        {
                            p.WaitForExit();
                            p.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }
#endif

                try
                {
                    ProcessStartInfo copyToAzureProc = new ProcessStartInfo();
                    copyToAzureProc.FileName = $"{AzCopyPath}";
                    copyToAzureProc.Arguments = $"{arguments}";
                    copyToAzureProc.UseShellExecute = false;
#if DEBUG
                    copyToAzureProc.RedirectStandardOutput = true;
                    copyToAzureProc.RedirectStandardError = true;
#endif

                    Logger.LogInformation($"StartContainerCopy:{containerInfo.ContainerName}");
                    sw.Start();
                    using (var p = Process.Start(copyToAzureProc))
                    {
                        p.WaitForExit();
#if DEBUG
                        var result = p.StandardOutput.ReadToEnd();
                        var error = p.StandardError.ReadToEnd();
                        Console.Write(result);
#endif
                        sw.Stop();
                        var exitCode = p.ExitCode;
                        Logger.LogInformation("EndContainerCopy:{container}:{exitCode}:{elapsedMilliseconds}", containerInfo.ContainerName, exitCode, sw.ElapsedMilliseconds);
                        p.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(LogEvents.CopyContainerFailed, ex, "Exception on backup save.");
                    return false;
                }

                try
                {
                    ProcessStartInfo copyToAzureProcLog = new ProcessStartInfo();
                    copyToAzureProcLog.FileName = $"{AzCopyPath}";
                    copyToAzureProcLog.Arguments = $"{argumentsLog}";
                    copyToAzureProcLog.UseShellExecute = false;
                    using (var pLog = Process.Start(copyToAzureProcLog))
                    {
                        pLog.WaitForExit();
                        pLog.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogEvents.CopyLogFailed, ex, "Exception on log save.");
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private void RefreshLogData(string logFolder)
        {
            if (Directory.Exists(logFolder))
            {
                Directory.Delete(logFolder, true);
            }
            Directory.CreateDirectory(logFolder);
        }

        private async Task<bool> TryCreateDestinationContainerAsync(string containerName)
        {
            try
            {
                var container = GetBlobContainerClient(_destStorageAccountName, _destStorageSasValue, containerName);
                await container.CreateIfNotExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(LogEvents.CreateContainerFailed, ex, "Exception on create container {containerName}.", containerName);
                return false;
            }
        }

        private BlobServiceClient GetBlobServiceClient(string storageAccountName, string storageSasToken)
        {
            var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/");

            if (_storageUseManagedIdentity && !string.IsNullOrEmpty(_managedIdentityClientId))
            {
                ManagedIdentityCredential msiCredential = new ManagedIdentityCredential(_managedIdentityClientId);
                return new BlobServiceClient(serviceUri, msiCredential);
            }
            else if (!string.IsNullOrEmpty(storageSasToken))
            {
                var sasCredential = new AzureSasCredential(storageSasToken);
                return new BlobServiceClient(serviceUri, sasCredential);
            }

            var credential = new DefaultAzureCredential();
            return new BlobServiceClient(serviceUri, credential);
        }

        private BlobContainerClient GetBlobContainerClient(string storageAccountName, string storageSasToken, string containerName)
        {
            BlobServiceClient blobService = GetBlobServiceClient(storageAccountName, storageSasToken);
            var container = blobService.GetBlobContainerClient(containerName);
            return container;
        }

        /// <summary>
        /// Deletes destination backup containers that are older than the backup days.
        /// </summary>
        /// <param name="containerName">The base container name</param>
        private async Task TryDeleteDestinationContainerAsync(string containerName)
        {
            var blobClient = GetBlobServiceClient(_destStorageAccountName, _destStorageSasValue);

            var containersToDelete = blobClient
                .GetBlobContainers(prefix: $"{GetContainerPrefix(containerName)}")
                .Where(blobContainer => blobContainer.Properties.LastModified < DateTime.UtcNow.AddDays(-_backupDays));

            foreach (var containerItem in containersToDelete)
            {
                try
                {
                    BlobContainerClient container = blobClient.GetBlobContainerClient(containerItem.Name);
                    await container.DeleteIfExistsAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogError(LogEvents.DeleteContainerFailed, ex, "Exception on delete container {containerName}.", containerItem.Name);
                }
            }
        }

        private string FormatDestinationContainerName(DateTimeOffset date, string containerName)
        {
            return $"{GetContainerPrefix(containerName)}{date.ToString("yyyyMMddHH")}";
        }

        private string GetContainerPrefix(string containerName)
        {
            return $"{containerName}-";
        }
    }
}
