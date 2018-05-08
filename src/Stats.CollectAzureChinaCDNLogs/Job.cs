// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CollectAzureChinaCDNLogs
{
    public class Job : JobBase
    {
        private const int DefaultExecutionTimeoutInSeconds = 14400; // 4 hours
        private const int MaxFilesToProcess = 4;

        private CloudStorageAccount _cloudStorageAccountSource;
        private CloudStorageAccount _cloudStorageAccountDestination;
        private string _cloudStorageContainerNameDestination;
        private string _cloudStorageContainerNameSource;
        private Collector _chinaCollector;
        private int _executionTimeoutInSeconds;
        private string _destinationFilePrefix;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var cloudStorageAccountConnStringSource = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureAccountConnectionStringSource);
            var cloudStorageAccountConnStringDest = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureAccountConnectionStringDestination);
            _cloudStorageAccountSource = ValidateAzureCloudStorageAccount(cloudStorageAccountConnStringSource);
            _cloudStorageAccountDestination = ValidateAzureCloudStorageAccount(cloudStorageAccountConnStringDest);
            _cloudStorageContainerNameDestination = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureContainerNameDestination);
            _cloudStorageContainerNameSource = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.AzureContainerNameSource);
            _destinationFilePrefix = JobConfigurationManager.GetArgument(jobArgsDictionary, ArgumentNames.DestinationFilePrefix);
            _executionTimeoutInSeconds = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, ArgumentNames.ExecutionTimeoutInSeconds) ?? DefaultExecutionTimeoutInSeconds;

            var source = new AzureStatsLogSource(cloudStorageAccountConnStringSource, _cloudStorageContainerNameSource, _executionTimeoutInSeconds/MaxFilesToProcess);
            var dest = new AzureStatsLogDestination(cloudStorageAccountConnStringDest,_cloudStorageContainerNameDestination);
            _chinaCollector = new ChinaStatsCollector(source, dest);
        }

        public override async Task Run()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(_executionTimeoutInSeconds*1000);
            var aggregateExceptions = await _chinaCollector.TryProcessAsync(maxFileCount: MaxFilesToProcess,
                 fileNameTransform: s => $"{_destinationFilePrefix}_{s}",
                 sourceContentType: ContentType.GZip,
                 destinationContentType: ContentType.GZip,
                 token: cts.Token);

            if (aggregateExceptions != null)
            {
                foreach(var ex in aggregateExceptions.InnerExceptions)
                {
                    Logger.LogError(Stats.AzureCdnLogs.Common.LogEvents.JobRunFailed, ex, ex.Message);
                }
            }

            if(cts.IsCancellationRequested)
            {
                Logger.LogInformation($"Execution exceeded the timeout of {_executionTimeoutInSeconds} seconds and it was cancelled.");
            }
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }
            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }
    }
}
