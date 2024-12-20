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
using Azure.Storage;
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
        private const string AzCopyPath = @"tools\azcopy\azCopy.exe";
        private readonly int DefaultBackupDays = -1;
        private string _managedIdentityClientId;
        private bool _storageUseManagedIdentity;
        private string _destStorageAccountName;
        private string _destStorageKeyValue;
        private string _destStorageSasValue;
        private int _backupDays;

        private IEnumerable<AzureContainerInfo> _sourceContainers;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            var jobConfiguration = _serviceProvider.GetRequiredService<IOptionsSnapshot<CopyAzureContainerConfiguration>>().Value;

            _backupDays = jobConfiguration.BackupDays ?? DefaultBackupDays;
            _destStorageAccountName = jobConfiguration.DestStorageAccountName ?? throw new InvalidOperationException(nameof(jobConfiguration.DestStorageAccountName) + " is required.");
            _destStorageKeyValue = jobConfiguration.DestStorageKeyValue;
            _destStorageSasValue = jobConfiguration.DestStorageSasValue;

            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            _managedIdentityClientId = configuration.GetValue<string>(Constants.ManagedIdentityClientIdKey);
            _storageUseManagedIdentity = configuration.GetValue<bool>(Constants.StorageUseManagedIdentityPropertyName);
            if (!_storageUseManagedIdentity && string.IsNullOrEmpty(_destStorageKeyValue) && string.IsNullOrEmpty(_destStorageSasValue))
            {
                throw new ArgumentException($"One of {nameof(jobConfiguration.DestStorageKeyValue)} or {nameof(jobConfiguration.DestStorageSasValue)} should be defined.");
            }

            if (_storageUseManagedIdentity)
            {
#if DEBUG
                Environment.SetEnvironmentVariable("AZCOPY_AUTO_LOGIN_TYPE", "DEVICE");
#else
                Environment.SetEnvironmentVariable("AZCOPY_AUTO_LOGIN_TYPE", "MSI");
                Environment.SetEnvironmentVariable("AZCOPY_MSI_CLIENT_ID", _managedIdentityClientId);
#endif
            }
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
            foreach (AzureContainerInfo sourceContainer in _sourceContainers)
            {
                if (_backupDays > 0)
                {
                    await TryDeleteContainerAsync(_destStorageAccountName, _destStorageKeyValue, _destStorageSasValue, sourceContainer.ContainerName, _backupDays);
                }

                await TryCopyContainerAsync(sourceContainer, currentDate);
            }
        }

        private async Task<bool> TryCopyContainerAsync(AzureContainerInfo containerInfo, DateTimeOffset date)
        {
            var sw = new Stopwatch();
            var azCopyTempFolder = $@"{Directory.GetCurrentDirectory()}\azCopy_{containerInfo.ContainerName}";
            var destContainer = FormatDestinationContainerName(date, containerInfo.ContainerName);
            var logFile = $"{destContainer}.log";
            var azCopyLogPath = Path.Combine(azCopyTempFolder, logFile);
            RefreshLogData(azCopyTempFolder, azCopyLogPath);

            if (await TryCreateDestinationContainerAsync(destContainer, _destStorageAccountName, _destStorageKeyValue, _destStorageSasValue))
            {
                var arguments = $"/Source:https://{containerInfo.StorageAccountName}.blob.core.windows.net/{containerInfo.ContainerName}/ " +
                                $"/Dest:https://{_destStorageAccountName}.blob.core.windows.net/{destContainer}/ ";
                var argumentsLog = $"/Source:{azCopyTempFolder} /Dest:https://{_destStorageAccountName}.blob.core.windows.net/logs ";

                var hasKeyOrSasToken = new string[] {
                    _destStorageSasValue,
                    _destStorageKeyValue,
                    containerInfo.StorageSasToken,
                    containerInfo.StorageAccountKey }
                .Any(credential => !string.IsNullOrEmpty(credential));

                if (hasKeyOrSasToken)
                {
                    var destCredential = string.IsNullOrEmpty(_destStorageSasValue) ? $"/DestKey:{_destStorageKeyValue} " : $"/DestSAS:{_destStorageSasValue} ";
                    var sourceCredential = string.IsNullOrEmpty(containerInfo.StorageSasToken) ? $"/SourceKey:{containerInfo.StorageAccountKey} " : $"/SourceSAS:{containerInfo.StorageSasToken} ";
                    arguments += $"{sourceCredential} {destCredential} ";
                    argumentsLog += $"{destCredential} ";
                }

                arguments += $"/Y /S /Z:{azCopyTempFolder} /V:{azCopyLogPath}";
                argumentsLog += "/destType:blob /Pattern:{logFile} /Y";

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

        private void RefreshLogData(string logFolder, string logFile)
        {
            if (Directory.Exists(logFolder))
            {
                Directory.Delete(logFolder, true);
            }
            Directory.CreateDirectory(logFolder);
            using (var stream = File.Create(logFile))
            {
                stream.Close();
            }
        }

        private async Task<bool> TryCreateDestinationContainerAsync(string containerName, string storageAccountName, string storageAccountKey, string storageSasToken)
        {
            try
            {
                var container = GetBlobContainerClient(storageAccountName, storageAccountKey, storageSasToken, containerName);
                await container.CreateIfNotExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(LogEvents.CreateContainerFailed, ex, "Exception on create container {containerName}.", containerName);
                return false;
            }
        }

        private BlobServiceClient GetBlobServiceClient(string storageAccountName, string storageAccountKey, string storageSasToken)
        {
            var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/");

            if (_storageUseManagedIdentity)
            {
                DefaultAzureCredential msiCredential = new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = _managedIdentityClientId
                    }
                );

                return new BlobServiceClient(serviceUri, msiCredential);
            }
            else if (string.IsNullOrEmpty(storageAccountKey))
            {
                var sasCredential = new AzureSasCredential(storageSasToken);
                return new BlobServiceClient(serviceUri, sasCredential);
            }

            var keyCredential = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
            return new BlobServiceClient(serviceUri, keyCredential);
        }

        private BlobContainerClient GetBlobContainerClient(string storageAccountName, string storageAccountKey, string storageSasToken, string containerName)
        {
            BlobServiceClient blobService = GetBlobServiceClient(storageAccountName, storageAccountKey, storageSasToken);
            var container = blobService.GetBlobContainerClient(containerName);
            return container;
        }

        /// <summary>
        /// For a specific base container 
        /// returns the backup containers that need to be deleted 
        /// </summary>
        /// <param name="storageAccountName">The storage account name</param>
        /// <param name="storageAccountKey">The storage account key</param>
        /// <param name="containerName">The base container name</param>
        /// <param name="backupDays">The number of days to keep</param>
        /// <returns></returns>
        private async Task TryDeleteContainerAsync(string storageAccountName,
                                                              string storageAccountKey,
                                                              string storageSasToken,
                                                              string containerName,
                                                              int backupDays)
        {
            var blobClient = GetBlobServiceClient(storageAccountName, storageAccountKey, storageSasToken);
            //it is used backupDays - 1 because a new container will be created
            var containersToDelete = blobClient.GetBlobContainers(prefix: $"{GetContainerPrefix(containerName)}").
                OrderByDescending(blobContainer => blobContainer.Properties.LastModified).
                Skip(backupDays - 1);

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
