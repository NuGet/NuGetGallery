// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class CreateAzureCdnWarehouseReportsJob : JsonConfigurationJob
    {
        private const int DefaultPerPackageReportDegreeOfParallelism = 8; // Generate 
        private const int DefaultSqlCommandTimeoutSeconds = 1800; // 30 minute SQL command timeout by default
        private const string _recentPopularityDetailByPackageReportBaseName = "recentpopularitydetail_";

        private CloudStorageAccount _cloudStorageAccount;
        private CloudStorageAccount _dataStorageAccount;
        private string _statisticsContainerName;
        private string _reportNameConfig;
        private string[] _dataContainerNames;
        private int _sqlCommandTimeoutSeconds = DefaultSqlCommandTimeoutSeconds;
        private int _perPackageReportDegreeOfParallelism = DefaultPerPackageReportDegreeOfParallelism;
        private ApplicationInsightsHelper _applicationInsightsHelper;

        private static readonly IDictionary<string, string> _storedProcedures = new Dictionary<string, string>
        {
            { ReportNames.NuGetClientVersion, "[dbo].[DownloadReportNuGetClientVersion]" },
            { ReportNames.Last6Weeks, "[dbo].[DownloadReportLast6Weeks]" },
            { ReportNames.RecentCommunityPopularity, "[dbo].[DownloadReportRecentCommunityPopularity]" },
            { ReportNames.RecentCommunityPopularityDetail, "[dbo].[DownloadReportRecentCommunityPopularityDetail]" },
            { ReportNames.RecentPopularity, "[dbo].[DownloadReportRecentPopularity]" },
            { ReportNames.RecentPopularityDetail, "[dbo].[DownloadReportRecentPopularityDetail]" },
        };

        private static readonly IDictionary<string, string> _storedProceduresPerPackageId = new Dictionary<string, string>
        {
            { ReportNames.RecentPopularityDetailByPackageId, "[dbo].[DownloadReportRecentPopularityDetailByPackage]" }
        };


        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            var configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<CreateAzureCdnWarehouseReportsConfiguration>>().Value;

            _sqlCommandTimeoutSeconds = configuration.CommandTimeOut ?? DefaultSqlCommandTimeoutSeconds;

            _perPackageReportDegreeOfParallelism = configuration.PerPackageReportDegreeOfParallelism ?? DefaultPerPackageReportDegreeOfParallelism;

            _cloudStorageAccount = ValidateAzureCloudStorageAccount(
                configuration.AzureCdnCloudStorageAccount,
                nameof(configuration.AzureCdnCloudStorageAccount));

            _statisticsContainerName = ValidateAzureContainerName(
                configuration.AzureCdnCloudStorageContainerName,
                nameof(configuration.AzureCdnCloudStorageContainerName));

            _dataStorageAccount = ValidateAzureCloudStorageAccount(
                configuration.DataStorageAccount,
                nameof(configuration.DataStorageAccount));

            _reportNameConfig = ValidateReportName(
                configuration.ReportName,
                nameof(configuration.ReportName));

            var containerNames = configuration.DataContainerName
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var containerName in containerNames)
            {
                ValidateAzureContainerName(containerName, nameof(configuration.DataContainerName));
            }

            _dataContainerNames = containerNames;
            _applicationInsightsHelper = new ApplicationInsightsHelper(ApplicationInsightsConfiguration.TelemetryConfiguration);
        }

        private bool ShouldGenerateReport(string reportName, string reportNameConfig)
        {
            return string.IsNullOrEmpty(reportNameConfig) || reportNameConfig.Equals(reportName);
        }

        private async Task GenerateStandardReport(
            string reportName,
            DateTime reportGenerationTime,
            CloudBlobContainer destinationContainer,
            ILogger<ReportBuilder> reportBuilderLogger,
            ILogger<ReportDataCollector> reportCollectorLogger)
        {
            var stopwatch = Stopwatch.StartNew();

            var reportBuilder = new ReportBuilder(reportBuilderLogger, reportName);
            var reportDataCollector = new ReportDataCollector(reportCollectorLogger, _storedProcedures[reportName], OpenSqlConnectionAsync<StatisticsDbConfiguration>, _sqlCommandTimeoutSeconds);

            await ProcessReport(LoggerFactory, destinationContainer, reportBuilder, reportDataCollector, reportGenerationTime);

            stopwatch.Stop();

            var reportMetricName = reportName + " report";
            _applicationInsightsHelper.TrackMetric(reportMetricName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
            _applicationInsightsHelper.TrackReportProcessed(reportMetricName);
        }

        public override async Task Run()
        {
            var statisticsDatabase = GetDatabaseRegistration<StatisticsDbConfiguration>();

            var reportGenerationTime = DateTime.UtcNow;
            var destinationContainer = _cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(_statisticsContainerName);

            Logger.LogDebug("Generating reports from {DataSource}/{InitialCatalog} and saving to {AccountName}/{Container}",
                statisticsDatabase.DataSource, statisticsDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

            var reportBuilderLogger = LoggerFactory.CreateLogger<ReportBuilder>();
            var reportCollectorLogger = LoggerFactory.CreateLogger<ReportDataCollector>();

            if (string.IsNullOrEmpty(_reportNameConfig))
            {
                // generate all reports
                foreach (var reportName in ReportNames.StandardReports)
                {
                    await GenerateStandardReport(reportName, reportGenerationTime, destinationContainer, reportBuilderLogger, reportCollectorLogger);
                }

                await RebuildPackageReports(destinationContainer, reportGenerationTime);
                await CleanInactiveRecentPopularityDetailByPackageReports(destinationContainer, reportGenerationTime);
            }
            else if (ReportNames.StandardReports.Contains(_reportNameConfig))
            {
                // generate only the specific standard report
                await GenerateStandardReport(_reportNameConfig, reportGenerationTime, destinationContainer, reportBuilderLogger, reportCollectorLogger);
            }
            else if (ShouldGenerateReport(ReportNames.RecentPopularityDetailByPackageId, _reportNameConfig))
            {
                await RebuildPackageReports(destinationContainer, reportGenerationTime);
                await CleanInactiveRecentPopularityDetailByPackageReports(destinationContainer, reportGenerationTime);
            }

            Logger.LogInformation("Generated reports from {DataSource}/{InitialCatalog} and saving to {AccountName}/{Container}",
                statisticsDatabase.DataSource, statisticsDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

            // totals reports
            Stopwatch stopwatch;

            // build downloads.v1.json
            if (ShouldGenerateReport(ReportNames.DownloadCount, _reportNameConfig))
            {
                stopwatch = Stopwatch.StartNew();

                var targets = new List<StorageContainerTarget>();
                targets.Add(new StorageContainerTarget(_cloudStorageAccount, _statisticsContainerName));
                foreach (var dataContainerName in _dataContainerNames)
                {
                    targets.Add(new StorageContainerTarget(_dataStorageAccount, dataContainerName));
                }

                var downloadCountReport = new DownloadCountReport(
                    LoggerFactory.CreateLogger<DownloadCountReport>(),
                    targets,
                    OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    _sqlCommandTimeoutSeconds);
                await downloadCountReport.Run();

                stopwatch.Stop();
                var reportMetricName = ReportNames.DownloadCount + ReportNames.Extension;
                _applicationInsightsHelper.TrackMetric(reportMetricName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                _applicationInsightsHelper.TrackReportProcessed(reportMetricName);
            }

            // build stats-totals.json
            if (ShouldGenerateReport(ReportNames.GalleryTotals, _reportNameConfig))
            {
                stopwatch = Stopwatch.StartNew();

                var galleryTotalsReport = new GalleryTotalsReport(
                    LoggerFactory.CreateLogger<GalleryTotalsReport>(),
                    _cloudStorageAccount,
                    _statisticsContainerName,
                    OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    commandTimeoutSeconds: _sqlCommandTimeoutSeconds);
                await galleryTotalsReport.Run();

                stopwatch.Stop();
                var reportMetricName = ReportNames.GalleryTotals + ReportNames.Extension;
                _applicationInsightsHelper.TrackMetric(reportMetricName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                _applicationInsightsHelper.TrackReportProcessed(reportMetricName);
            }

            // build tools.v1.json
            if (ShouldGenerateReport(ReportNames.DownloadsPerToolVersion, _reportNameConfig))
            {
                stopwatch = Stopwatch.StartNew();

                var toolsReport = new DownloadsPerToolVersionReport(
                    LoggerFactory.CreateLogger<DownloadsPerToolVersionReport>(),
                    _cloudStorageAccount,
                    _statisticsContainerName,
                    OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    _sqlCommandTimeoutSeconds);
                await toolsReport.Run();

                stopwatch.Stop();
                var reportMetricName = ReportNames.DownloadsPerToolVersion + ReportNames.Extension;
                _applicationInsightsHelper.TrackMetric(reportMetricName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                _applicationInsightsHelper.TrackReportProcessed(reportMetricName);
            }
        }

        private static async Task ProcessReport(ILoggerFactory loggerFactory, CloudBlobContainer destinationContainer, ReportBuilder reportBuilder,
            ReportDataCollector reportDataCollector, DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            var dataTable = await reportDataCollector.CollectAsync(reportGenerationTime, parameters);
            if (dataTable.Rows.Count == 0)
            {
                return;
            }

            var json = reportBuilder.CreateReport(dataTable);

            var reportWriter = new ReportWriter(loggerFactory.CreateLogger<ReportWriter>(), destinationContainer);
            await reportWriter.WriteReport(reportBuilder.ReportArtifactName, json);
        }

        private async Task RebuildPackageReports(CloudBlobContainer destinationContainer, DateTime reportGenerationTime)
        {
            var dirtyPackageIds = await ReportDataCollector.GetDirtyPackageIds(
                LoggerFactory.CreateLogger<ReportDataCollector>(),
                OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                reportGenerationTime,
                _sqlCommandTimeoutSeconds);

            if (!dirtyPackageIds.Any())
            {
                return;
            }

            // first process the top 100 packages
            var top100 = dirtyPackageIds.Take(100);
            var reportDataCollector = new ReportDataCollector(
                LoggerFactory.CreateLogger<ReportDataCollector>(),
                _storedProceduresPerPackageId[ReportNames.RecentPopularityDetailByPackageId],
                OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                _sqlCommandTimeoutSeconds);

            var top100Task = Parallel.ForEach(top100, new ParallelOptions { MaxDegreeOfParallelism = _perPackageReportDegreeOfParallelism }, dirtyPackageId =>
            {
                var packageId = dirtyPackageId.PackageId.ToLowerInvariant();
                var reportBuilder = new RecentPopularityDetailByPackageReportBuilder(
                    LoggerFactory.CreateLogger<RecentPopularityDetailByPackageReportBuilder>(),
                    ReportNames.RecentPopularityDetailByPackageId,
                    "recentpopularity/" + _recentPopularityDetailByPackageReportBaseName + packageId);

                ProcessReport(LoggerFactory, destinationContainer, reportBuilder, reportDataCollector, reportGenerationTime, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
                _applicationInsightsHelper.TrackReportProcessed(reportBuilder.ReportName + " report", packageId);
            });

            // once top 100 is processed, continue with the rest
            if (top100Task.IsCompleted)
            {
                var excludingTop100 = dirtyPackageIds.Skip(100);

                top100Task = Parallel.ForEach(excludingTop100, new ParallelOptions { MaxDegreeOfParallelism = _perPackageReportDegreeOfParallelism },
                    dirtyPackageId =>
                    {
                        // generate all reports
                        var reportGenerators = new Dictionary<ReportBuilder, ReportDataCollector>
                        {
                            {
                                new RecentPopularityDetailByPackageReportBuilder(
                                    LoggerFactory.CreateLogger<RecentPopularityDetailByPackageReportBuilder>(),
                                    ReportNames.RecentPopularityDetailByPackageId,
                                    "recentpopularity/" + _recentPopularityDetailByPackageReportBaseName +
                                    dirtyPackageId.PackageId.ToLowerInvariant()),
                                new ReportDataCollector(
                                    LoggerFactory.CreateLogger<ReportDataCollector>(),
                                    _storedProceduresPerPackageId[ReportNames.RecentPopularityDetailByPackageId],
                                    OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                                    _sqlCommandTimeoutSeconds)
                            }
                        };

                        foreach (var reportGenerator in reportGenerators)
                        {
                            ProcessReport(LoggerFactory, destinationContainer, reportGenerator.Key, reportGenerator.Value,
                                reportGenerationTime, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
                            _applicationInsightsHelper.TrackReportProcessed(reportGenerator.Key.ReportName + " report",
                                dirtyPackageId.PackageId.ToLowerInvariant());
                        }
                    });

                if (top100Task.IsCompleted)
                {
                    var runToCursor = dirtyPackageIds.First().RunToCuror;
                    await ReportDataCollector.UpdateDirtyPackageIdCursor(OpenSqlConnectionAsync<StatisticsDbConfiguration>, runToCursor, _sqlCommandTimeoutSeconds);
                }
            }
        }

        private async Task CleanInactiveRecentPopularityDetailByPackageReports(CloudBlobContainer destinationContainer, DateTime reportGenerationTime)
        {
            Logger.LogDebug("Getting list of inactive packages.");
            var packageIds = await ReportDataCollector.ListInactivePackageIdReports(
                OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                reportGenerationTime,
                _sqlCommandTimeoutSeconds);

            Logger.LogInformation("Found {InactivePackageCount} inactive packages.", packageIds.Count);

            // Collect the list of reports
            var subContainer = "recentpopularity/";
            Logger.LogDebug("Collecting list of package detail reports");
            var reports = destinationContainer.ListBlobs(subContainer + _recentPopularityDetailByPackageReportBaseName)
                    .OfType<CloudBlockBlob>()
                    .Select(b => b.Name);

            var reportSet = new HashSet<string>(reports);
            Logger.LogInformation("Collected {PackageDetailReportCount} package detail reports", reportSet.Count);

            Parallel.ForEach(packageIds, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async id =>
             {
                 string reportName = _recentPopularityDetailByPackageReportBaseName + id;
                 string blobName = subContainer + reportName + ReportNames.Extension;
                 if (reportSet.Contains(blobName))
                 {
                     var blob = destinationContainer.GetBlockBlobReference(blobName);
                     Logger.LogDebug("{ReportName}: Deleting empty report from {BlobUri}", reportName, blob.Uri.AbsoluteUri);

                     await blob.DeleteIfExistsAsync();

                     Logger.LogInformation("{ReportName}: Deleted empty report from {BlobUri}", reportName, blob.Uri.AbsoluteUri);
                 }
             });
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount, string configurationName)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException($"Job configuration {configurationName} is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
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

        private static string ValidateReportName(string reportName, string configurationName)
        {
            if (string.IsNullOrWhiteSpace(reportName))
            {
                return null;
            }

            var normalizedReportName = reportName.ToLowerInvariant();
            if (!ReportNames.AllReports.Contains(normalizedReportName))
            {
                throw new ArgumentException($"Job configuration {configurationName} contains unknown report name.");
            }

            return normalizedReportName;
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