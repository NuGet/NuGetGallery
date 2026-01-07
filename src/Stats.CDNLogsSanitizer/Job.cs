// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Storage.Blobs;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CDNLogsSanitizer
{
    public class Job : JsonConfigurationJob
    {
        private const int DefaultExecutionTimeoutInSeconds = 345600; // 10 days
        private const int DefaultMaxBlobsToProcess = 4;

        private JobConfiguration _configuration;
        private int _executionTimeoutInSeconds;
        private int _maxBlobsToProcess;
        private LogHeaderMetadata _logHeaderMetadata;
        private Processor _processor;
        private string _blobPrefix;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            InitializeJobConfiguration(_serviceProvider);
        }

        public void InitializeJobConfiguration(IServiceProvider serviceProvider)
        {
            _configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<JobConfiguration>>().Value;
            _executionTimeoutInSeconds = _configuration.ExecutionTimeoutInSeconds ?? DefaultExecutionTimeoutInSeconds;
            _maxBlobsToProcess = _configuration.MaxBlobsToProcess ?? DefaultMaxBlobsToProcess;
            var logHeader = _configuration.LogHeader ?? throw new ArgumentNullException(nameof(_configuration.LogHeader));
            var logHeaderDelimiter = _configuration.LogHeaderDelimiter ?? throw new ArgumentNullException(nameof(_configuration.LogHeaderDelimiter));
            _logHeaderMetadata = new LogHeaderMetadata(logHeader, logHeaderDelimiter);
            _blobPrefix = _configuration.BlobPrefix ;

            var connectionStringSource = _configuration.AzureAccountConnectionStringSource.Replace("SharedAccessSignature=?", "SharedAccessSignature=");
            var connectionStringDestination = _configuration.AzureAccountConnectionStringDestination.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

            var blobLeaseManager = new AzureBlobLeaseManager(
                serviceProvider.GetRequiredService<ILogger<AzureBlobLeaseManager>>());

            var source = new AzureStatsLogSource(
                ValidateAzureCloudStorageAccount(connectionStringSource),
                _configuration.AzureContainerNameSource,
                _executionTimeoutInSeconds / _maxBlobsToProcess,
                blobLeaseManager,
                serviceProvider.GetRequiredService<ILogger<AzureStatsLogSource>>());

            var dest = new AzureStatsLogDestination(
                ValidateAzureCloudStorageAccount(connectionStringDestination),
                _configuration.AzureContainerNameDestination,
                serviceProvider.GetRequiredService<ILogger<AzureStatsLogDestination>>());

            var sanitizers = new List<ISanitizer>{ new ClientIPSanitizer(_logHeaderMetadata) };

            _processor = new Processor(source, dest, _maxBlobsToProcess, sanitizers, serviceProvider.GetRequiredService<ILogger<Processor>>());
        }

        public override async Task Run()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(_executionTimeoutInSeconds * 1000);
                await _processor.ProcessAsync(cts.Token, _blobPrefix);

                if (cts.IsCancellationRequested)
                {
                    Logger.LogInformation("Execution exceeded the timeout of {ExecutionTimeoutInSeconds} seconds and it was cancelled.", _executionTimeoutInSeconds);
                }
            }
        }

        private static BlobServiceClient ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            try
            {
                var account = new BlobServiceClient(cloudStorageAccount);
                return account;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.", ex);
            }            
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<JobConfiguration>(services, configurationRoot);
        }
    }
}
