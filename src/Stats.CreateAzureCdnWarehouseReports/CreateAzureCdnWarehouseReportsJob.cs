// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class CreateAzureCdnWarehouseReportsJob : JsonConfigurationJob
    {
        private const int DefaultSqlCommandTimeoutSeconds = 1800; // 30 minute SQL command timeout by default

        private CloudStorageAccount _cloudStorageAccount;
        private CloudStorageAccount _additionalGalleryTotalsAccount;
        private string _statisticsContainerName;
        private string _additionalGalleryTotalsContainerName;
        private int _sqlCommandTimeoutSeconds = DefaultSqlCommandTimeoutSeconds;
        private ApplicationInsightsHelper _applicationInsightsHelper;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            var configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<CreateAzureCdnWarehouseReportsConfiguration>>().Value;

            _sqlCommandTimeoutSeconds = configuration.CommandTimeOut ?? DefaultSqlCommandTimeoutSeconds;

            _cloudStorageAccount = ValidateAzureCloudStorageAccount(
                configuration.AzureCdnCloudStorageAccount,
                nameof(configuration.AzureCdnCloudStorageAccount));

            _statisticsContainerName = ValidateAzureContainerName(
                configuration.AzureCdnCloudStorageContainerName,
                nameof(configuration.AzureCdnCloudStorageContainerName));

            if (!string.IsNullOrWhiteSpace(configuration.AdditionalGalleryTotalsStorageAccount))
            {
                _additionalGalleryTotalsAccount = ValidateAzureCloudStorageAccount(
                    configuration.AdditionalGalleryTotalsStorageAccount,
                    nameof(configuration.AdditionalGalleryTotalsStorageAccount));
                Logger.LogInformation("Additional totals account found {BlobEndpoint}", _additionalGalleryTotalsAccount.BlobEndpoint.GetLeftPart(UriPartial.Path));

                _additionalGalleryTotalsContainerName = configuration.AdditionalGalleryTotalsStorageContainerName;
            }

            _applicationInsightsHelper = new ApplicationInsightsHelper(ApplicationInsightsConfiguration.TelemetryConfiguration);
        }

        public override async Task Run()
        {
            var reportGenerationTime = DateTime.UtcNow;
            var destinationContainer = _cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(_statisticsContainerName);

            Logger.LogInformation("Generating reports and saving to {AccountName}/{Container}",
                _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

            // build stats-totals.json
            var stopwatch = Stopwatch.StartNew();

            var targets = new List<StorageContainerTarget>
            {
                new(_cloudStorageAccount, _statisticsContainerName)
            };
            if (_additionalGalleryTotalsAccount != null && !string.IsNullOrWhiteSpace(_additionalGalleryTotalsContainerName))
            {
                targets.Add(new StorageContainerTarget(_additionalGalleryTotalsAccount, _additionalGalleryTotalsContainerName));
                Logger.LogInformation("Added additional target for stats totals report {BlobEndpoint}/{Container}",
                    _additionalGalleryTotalsAccount.BlobEndpoint.GetLeftPart(UriPartial.Path),
                    _additionalGalleryTotalsContainerName);
            }
            var galleryTotalsReport = new GalleryTotalsReport(
                LoggerFactory.CreateLogger<GalleryTotalsReport>(),
                targets,
                OpenSqlConnectionAsync<GalleryDbConfiguration>,
                commandTimeoutSeconds: _sqlCommandTimeoutSeconds);
            await galleryTotalsReport.Run();

            stopwatch.Stop();
            var reportMetricName = ReportNames.GalleryTotals + ReportNames.Extension;
            _applicationInsightsHelper.TrackMetric(reportMetricName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
            _applicationInsightsHelper.TrackReportProcessed(reportMetricName);
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount, string configurationName)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException($"Job configuration {configurationName} is not defined.");
            }

            if (CloudStorageAccount.TryParse(cloudStorageAccount, out CloudStorageAccount account))
            {
                return account;
            }

            throw new ArgumentException($"Job configuration {configurationName} is invalid.");
        }

        private static string ValidateAzureContainerName(string containerName, string configurationName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException($"Job configuration {configurationName} is not defined.");
            }

            return containerName;
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<CreateAzureCdnWarehouseReportsConfiguration>(services, configurationRoot);
        }
    }
}
