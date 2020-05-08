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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;

namespace CopyAzureContainer
{
    class CopyAzureContainerJob : JsonConfigurationJob
    {
        private const string SectionName = "CopyAzureContainer";
        private const string AzCopyPath = @"tools\azcopy\azCopy.exe";
        private readonly int DefaultBackupDays = -1;
        private string _destStorageAccountName;
        private string _destStorageKeyValue;
        private int _backupDays;

        private IEnumerable<AzureContainerInfo> _sourceContainers;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            var configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<CopyAzureContainerConfiguration>>().Value;

            _backupDays = configuration.BackupDays ?? DefaultBackupDays;
            _destStorageAccountName = configuration.DestStorageAccountName ?? throw new InvalidOperationException(nameof(configuration.DestStorageAccountName) + " is required.");
            _destStorageKeyValue = configuration.DestStorageKeyValue ?? throw new InvalidOperationException(nameof(configuration.DestStorageKeyValue) + " is required.");
            _sourceContainers = configuration.SourceContainers ?? throw new InvalidOperationException(nameof(configuration.SourceContainers) + " is required.");
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
            var deleteTasks = (_backupDays > 0) ? _sourceContainers.
                SelectMany( c => GetContainersToBeDeleted(_destStorageAccountName, _destStorageKeyValue, c.ContainerName, _backupDays) ).
                Select( c => TryDeleteContainerAsync(c)).ToArray() : null;

            foreach(var c in _sourceContainers)
            {
                await TryCopyContainerAsync(c.ContainerName, c.StorageAccountName, c.StorageAccountKey, currentDate);
            }
            if (deleteTasks != null)
            {
                Task.WaitAll(deleteTasks);
            }
        }

        private async Task<bool> TryCopyContainerAsync(string containerName, string sourceAccountName, string sourceAccountKey , DateTimeOffset date)
        {
            var sw = new Stopwatch();
            var azCopyTempFolder = $@"{Directory.GetCurrentDirectory()}\azCopy_{containerName}";
            var destContainer = FormatDestinationContainerName(date, containerName); 
            var logFile = $"{destContainer}.log";
            var azCopyLogPath = Path.Combine(azCopyTempFolder, logFile);
            RefreshLogData(azCopyTempFolder, azCopyLogPath);

            if (await TryCreateDestinationContainerAsync(destContainer, _destStorageAccountName, _destStorageKeyValue))
            {
                var arguments = $"/Source:https://{sourceAccountName}.blob.core.windows.net/{containerName}/ " +
                                   $"/Dest:https://{_destStorageAccountName}.blob.core.windows.net/{destContainer}/ " +
                                   $"/SourceKey:{sourceAccountKey} /DestKey:{_destStorageKeyValue} " +
                                   $"/Y /S /Z:{azCopyTempFolder} /V:{azCopyLogPath}";

                var argumentsLog = $"/Source:{azCopyTempFolder} /Dest:https://{_destStorageAccountName}.blob.core.windows.net/logs" +
                                      $" /DestKey:{_destStorageKeyValue} /destType:blob /Pattern:{logFile} /Y";

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

                    Logger.LogInformation($"StartContainerCopy:{containerName}");
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
                        Logger.LogInformation("EndContainerCopy:{container}:{exitCode}:{elapsedMilliseconds}", containerName, exitCode, sw.ElapsedMilliseconds);
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

        private async Task<bool> TryCreateDestinationContainerAsync(string containerName, string storageAccountName, string storageAccountKey)
        {
            try
            {
                var container = GetCloudBlobContainer(storageAccountName, storageAccountKey, containerName);
                await container.CreateIfNotExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(LogEvents.CreateContainerFailed, ex, "Exception on create container {containerName}.", containerName);
                return false;
            }
        }

        private async Task<bool> TryDeleteContainerAsync(CloudBlobContainer container)
        {
            try
            {
                await container.DeleteIfExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(LogEvents.DeleteContainerFailed, ex, "Exception on delete container {containerName}.", container.Name);
                return false;
            }
        }

        private CloudBlobClient GetCloudBlobClient(string storageAccountName, string storageAccountKey)
        {
            var storageAccount = new CloudStorageAccount(new StorageCredentials(storageAccountName, storageAccountKey), useHttps: true);
            var blobClient = storageAccount.CreateCloudBlobClient();
            return blobClient;
        }

        private CloudBlobContainer GetCloudBlobContainer(string storageAccountName, string storageAccountKey, string containerName)
        {
            var blobClient = GetCloudBlobClient(storageAccountName, storageAccountKey);
            var container = blobClient.GetContainerReference(containerName);
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
        private IEnumerable<CloudBlobContainer> GetContainersToBeDeleted(string storageAccountName, 
                                                              string storageAccountKey, 
                                                              string containerName,
                                                              int backupDays)
        {
            var blobClient = GetCloudBlobClient(storageAccountName, storageAccountKey);
            //it is used backupDays - 1 because a new container will be created
            var containersToDelete = blobClient.ListContainers(prefix: $"{GetContainerPrefix(containerName)}").
                OrderByDescending( blobContainer => blobContainer.Properties.LastModified ).
                Skip(backupDays - 1); 
            return containersToDelete;
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
