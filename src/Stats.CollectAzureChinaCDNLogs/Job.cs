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
using NuGet.Jobs;
using Azure.Storage.Blobs;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;
using Azure.Identity;

namespace Stats.CollectAzureChinaCDNLogs
{
    public class Job : JsonConfigurationJob
    {
        private const int DefaultExecutionTimeoutInSeconds = 14400; // 4 hours
        private const int MaxFilesToProcess = 4;

        private CollectAzureChinaCdnLogsConfiguration _configuration;
        private int _executionTimeoutInSeconds;
        private Collector _chinaCollector;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            InitializeJobConfiguration(_serviceProvider);
        }

        public void InitializeJobConfiguration(IServiceProvider serviceProvider)
        {
            _configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<CollectAzureChinaCdnLogsConfiguration>>().Value;
            _executionTimeoutInSeconds = _configuration.ExecutionTimeoutInSeconds ?? DefaultExecutionTimeoutInSeconds;

            var connectionStringSource = _configuration.AzureAccountConnectionStringSource;
            var connectionStringDestination = _configuration.AzureAccountConnectionStringDestination;

            if (string.IsNullOrEmpty(connectionStringSource))
            {
                throw new ArgumentException(nameof(connectionStringSource));
            }

            if (string.IsNullOrEmpty(_configuration.AzureAccountConnectionStringDestination))
            {
                throw new ArgumentException(nameof(connectionStringDestination));
            }

            StorageMsiConfiguration storageMsiConfiguration = serviceProvider.GetRequiredService<IOptionsSnapshot<StorageMsiConfiguration>>().Value;

            var blobLeaseManager = new AzureBlobLeaseManager(serviceProvider.GetRequiredService<ILogger<AzureBlobLeaseManager>>());

            var source = new AzureStatsLogSource(
                ValidateAzureBlobServiceClient(connectionStringSource, storageMsiConfiguration),
                _configuration.AzureContainerNameSource,
                _executionTimeoutInSeconds / MaxFilesToProcess,
                blobLeaseManager,
                serviceProvider.GetRequiredService<ILogger<AzureStatsLogSource>>());

            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            connectionStringDestination = connectionStringDestination.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

            var dest = new AzureStatsLogDestination(
                ValidateAzureBlobServiceClient(connectionStringDestination, storageMsiConfiguration, isGlobal: true),
                _configuration.AzureContainerNameDestination,
                serviceProvider.GetRequiredService<ILogger<AzureStatsLogDestination>>());

            _chinaCollector = new ChinaStatsCollector(
                source,
                dest,
                serviceProvider.GetRequiredService<ILogger<ChinaStatsCollector>>(),
                _configuration.WriteOutputHeader,
                _configuration.AddSourceFilenameColumn);
        }

        public override async Task Run()
        {
            // StreamReader and StreamWriter used in Collector class to process logs
            // don't have ReadLineAsync/WriteLineAsync overloads that accept CancellationToken,
            // so we can't reliably terminate those if they get stuck or very slow. If migrated
            // to .NET 6+ then we can properly propagate the token and this hack would no longer
            // be needed.

            // Instead we'll wait a bit extra time after firing main CancellationTokenSource to
            // let it gracefully stop if it is indeed just slow, then stop the process and let it
            // retry processing on restart.
            var forceStopCts = new CancellationTokenSource();
            forceStopCts.CancelAfter(TimeSpan.FromSeconds(_executionTimeoutInSeconds + 60));

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(_executionTimeoutInSeconds));
            Task<AggregateException> logProcessTask = _chinaCollector
                .TryProcessAsync(maxFileCount: MaxFilesToProcess,
                     fileNameTransform: s => $"{_configuration.DestinationFilePrefix}_{s}",
                     sourceContentType: ContentType.GZip,
                     destinationContentType: ContentType.GZip,
                     token: cts.Token)
                .WithCancellation(forceStopCts.Token);

            AggregateException aggregateExceptions = null;

            try
            {
                aggregateExceptions = await logProcessTask;
            }
            catch (TaskCanceledException)
            {
                Logger.LogWarning("Got TaskCancelledException, terminating");
                return;
            }

            if (aggregateExceptions != null)
            {
                foreach (var ex in aggregateExceptions.InnerExceptions)
                {
                    Logger.LogError(LogEvents.JobRunFailed, ex, ex.Message);
                }
            }

            if (cts.IsCancellationRequested)
            {
                Logger.LogInformation("Execution exceeded the timeout of {ExecutionTimeoutInSeconds} seconds and it was cancelled.", _executionTimeoutInSeconds);
            }
        }

        /// <summary>
        /// Validates and creates a <see cref="BlobServiceClient"/> based on the provided connection string and MSI configuration.
        /// Uses SAS tokens for authentication for the source storage (because it is in China) and MSI for destination because
        /// it is in a non-China region.
        /// </summary>
        /// <param name="isGlobal">Indicates whether the client is using China storage or global storage. If true, MSI is used.</param> 
        private static BlobServiceClient ValidateAzureBlobServiceClient(string blobServiceClient, StorageMsiConfiguration msiConfiguration, bool isGlobal = false)
        {
            if (string.IsNullOrEmpty(blobServiceClient))
            {
                throw new ArgumentException("Job parameter for Azure CDN Blob Service Client is not defined.");
            }

            try
            {
                if (msiConfiguration.UseManagedIdentity && isGlobal)
                {
                    blobServiceClient = blobServiceClient.Replace("BlobEndPoint=", "");
                    Uri blobEndpointUri = new Uri(blobServiceClient);

                    if (string.IsNullOrWhiteSpace(msiConfiguration.ManagedIdentityClientId))
                    {
                        // 1. Using MSI with DefaultAzureCredential (local debugging)
                        return new BlobServiceClient(
                            blobEndpointUri,
                            new DefaultAzureCredential());
                    }
                    else
                    {
                        // 2. Using MSI with ClientId
                        return new BlobServiceClient(
                            blobEndpointUri,
                            new ManagedIdentityCredential(msiConfiguration.ManagedIdentityClientId));
                    }
                }
                else
                {
                    // 3. Using SAS token
                    // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
                    var connectionString = blobServiceClient.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

                    return new BlobServiceClient(connectionString);
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Job parameter for Azure CDN Blob Service Client is invalid.", ex);
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<CollectAzureChinaCdnLogsConfiguration>(services, configurationRoot);
            services.ConfigureStorageMsi(configurationRoot);
        }
    }
}
