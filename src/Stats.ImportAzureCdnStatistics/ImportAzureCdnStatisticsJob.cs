// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using Stats.AzureCdnLogs.Common;
using Stats.LogInterpretation;

namespace Stats.ImportAzureCdnStatistics
{
    public class ImportAzureCdnStatisticsJob : JsonConfigurationJob
    {
        private ImportAzureCdnStatisticsConfiguration _configuration;
        private AzureCdnPlatform _azureCdnPlatform;
        private CloudBlobClient _cloudBlobClient;
        private LogFileProvider _blobLeaseManager;
        private ApplicationInsightsHelper _applicationInsightsHelper;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<ImportAzureCdnStatisticsConfiguration>>().Value;

            _azureCdnPlatform = ValidateAzureCdnPlatform(_configuration.AzureCdnPlatform);

            var cloudStorageAccount = ValidateAzureCloudStorageAccount(_configuration.AzureCdnCloudStorageAccount);
            _cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            _cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            _blobLeaseManager = new LogFileProvider(
                _cloudBlobClient.GetContainerReference(_configuration.AzureCdnCloudStorageContainerName),
                LoggerFactory);

            _applicationInsightsHelper = new ApplicationInsightsHelper(ApplicationInsightsConfiguration.TelemetryConfiguration);
        }

        public override async Task Run()
        {
            // Get the target blob container (for archiving decompressed log files)
            var targetBlobContainer = _cloudBlobClient.GetContainerReference(
                _configuration.AzureCdnCloudStorageContainerName + "-archive");
            await targetBlobContainer.CreateIfNotExistsAsync();

            // Get the dead-letter table (corrupted or failed blobs will end up there)
            var deadLetterBlobContainer = _cloudBlobClient.GetContainerReference(
                _configuration.AzureCdnCloudStorageContainerName + "-deadletter");
            await deadLetterBlobContainer.CreateIfNotExistsAsync();

            // Create a parser
            var warehouse = new Warehouse(
                LoggerFactory,
                OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                _applicationInsightsHelper);
            var statisticsBlobContainerUtility = new StatisticsBlobContainerUtility(
                targetBlobContainer,
                deadLetterBlobContainer,
                LoggerFactory,
                _applicationInsightsHelper);

            var logProcessor = new LogFileProcessor(
                statisticsBlobContainerUtility,
                LoggerFactory,
                warehouse,
                _applicationInsightsHelper);

            // Get the next to-be-processed raw log file using the cdn raw log file name prefix
            var prefix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_",
                _azureCdnPlatform.GetRawLogFilePrefix(),
                _configuration.AzureCdnAccountNumber);

            // Get next raw log file to be processed
            IReadOnlyCollection<string> alreadyAggregatedLogFiles = null;
            if (_configuration.AggregatesOnly)
            {
                // We only want to process aggregates for the log files.
                // Get the list of files we already processed so we can skip them.
                alreadyAggregatedLogFiles = await warehouse.GetAlreadyAggregatedLogFilesAsync();
            }

            var leasedLogFiles = await _blobLeaseManager.LeaseNextLogFilesToBeProcessedAsync(prefix, alreadyAggregatedLogFiles);
            foreach (var leasedLogFile in leasedLogFiles)
            {
                var packageTranslator = new PackageTranslator();
                var packageStatisticsParser = new PackageStatisticsParser(packageTranslator, LoggerFactory);
                await logProcessor.ProcessLogFileAsync(leasedLogFile, packageStatisticsParser, _configuration.AggregatesOnly);

                if (_configuration.AggregatesOnly)
                {
                    _blobLeaseManager.TrackLastProcessedBlobUri(leasedLogFile.Uri);
                }

                leasedLogFile.Dispose();
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

        private static AzureCdnPlatform ValidateAzureCdnPlatform(string azureCdnPlatform)
        {
            if (string.IsNullOrEmpty(azureCdnPlatform))
            {
                throw new ArgumentException("Job parameter for Azure CDN Platform is not defined.");
            }

            AzureCdnPlatform value;
            if (Enum.TryParse(azureCdnPlatform, true, out value))
            {
                return value;
            }
            throw new ArgumentException("Job parameter for Azure CDN Platform is invalid. Allowed values are: HttpLargeObject, HttpSmallObject, ApplicationDeliveryNetwork, FlashMediaStreaming.");
        }

        private static string ValidateAzureContainerName(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Job parameter for Azure Storage Container Name is not defined.");
            }
            return containerName;
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<ImportAzureCdnStatisticsConfiguration>(services, configurationRoot);
        }
    }
}