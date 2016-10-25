// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class Job
        : JobBase
    {
        private const string _recentPopularityDetailByPackageReportBaseName = "recentpopularitydetail_";
        private CloudStorageAccount _cloudStorageAccount;
        private CloudStorageAccount _dataStorageAccount;
        private string _statisticsContainerName;
        private SqlConnectionStringBuilder _statisticsDatabase;
        private SqlConnectionStringBuilder _galleryDatabase;
        private string _reportName;
        private string[] _dataContainerNames;
        private ILogger _logger;

        private static readonly IDictionary<string, string> _storedProcedures = new Dictionary<string, string>
        {
            {ReportNames.NuGetClientVersion, "[dbo].[DownloadReportNuGetClientVersion]" },
            {ReportNames.Last6Weeks, "[dbo].[DownloadReportLast6Weeks]" },
            {ReportNames.RecentPopularity, "[dbo].[DownloadReportRecentPopularity]" },
            {ReportNames.RecentPopularityDetail, "[dbo].[DownloadReportRecentPopularityDetail]" },
        };

        private static readonly IDictionary<string, string> _storedProceduresPerPackageId = new Dictionary<string, string>
        {
            {ReportNames.RecentPopularityDetailByPackageId, "[dbo].[DownloadReportRecentPopularityDetailByPackage]" }
        };


        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = jobArgsDictionary.GetOrNull(JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerFactory = LoggingSetup.CreateLoggerFactory();
                _logger = loggerFactory.CreateLogger<Job>();

                var cloudStorageAccountConnectionString = jobArgsDictionary[JobArgumentNames.AzureCdnCloudStorageAccount];
                var statisticsDatabaseConnectionString = jobArgsDictionary[JobArgumentNames.StatisticsDatabase];
                var galleryDatabaseConnectionString = jobArgsDictionary[JobArgumentNames.SourceDatabase];
                var dataStorageAccountConnectionString = jobArgsDictionary[JobArgumentNames.DataStorageAccount];

                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString, JobArgumentNames.AzureCdnCloudStorageAccount);
                _statisticsContainerName = ValidateAzureContainerName(jobArgsDictionary[JobArgumentNames.AzureCdnCloudStorageContainerName], JobArgumentNames.AzureCdnCloudStorageContainerName);
                _dataStorageAccount = ValidateAzureCloudStorageAccount(dataStorageAccountConnectionString, JobArgumentNames.DataStorageAccount);
                _reportName = ValidateReportName(jobArgsDictionary.GetOrNull(JobArgumentNames.WarehouseReportName));
                _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);
                _galleryDatabase = new SqlConnectionStringBuilder(galleryDatabaseConnectionString);

                var containerNames = jobArgsDictionary[JobArgumentNames.DataContainerName]
                        .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var containerName in containerNames)
                {
                    ValidateAzureContainerName(containerName, JobArgumentNames.DataContainerName);
                }

                _dataContainerNames = containerNames;
            }
            catch (Exception exception)
            {
                _logger.LogError("Failed to initialize job! {Exception}", exception);

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            try
            {
                var reportGenerationTime = DateTime.UtcNow;
                var destinationContainer = _cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(_statisticsContainerName);

                _logger.LogDebug("Generating reports from {DataSource}/{InitialCatalog} and saving to {AccountName}/{Container}", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

                if (string.IsNullOrEmpty(_reportName))
                {
                    // generate all reports
                    var reportGenerators = new Dictionary<ReportBuilder, ReportDataCollector>
                    {
                        { new ReportBuilder(ReportNames.NuGetClientVersion), new ReportDataCollector(_storedProcedures[ReportNames.NuGetClientVersion], _statisticsDatabase) },
                        { new ReportBuilder(ReportNames.Last6Weeks), new ReportDataCollector(_storedProcedures[ReportNames.Last6Weeks], _statisticsDatabase) },
                        { new ReportBuilder(ReportNames.RecentPopularity), new ReportDataCollector(_storedProcedures[ReportNames.RecentPopularity], _statisticsDatabase) },
                        { new ReportBuilder(ReportNames.RecentPopularityDetail), new ReportDataCollector(_storedProcedures[ReportNames.RecentPopularityDetail], _statisticsDatabase) }
                    };

                    foreach (var reportGenerator in reportGenerators)
                    {
                        await ProcessReport(destinationContainer, reportGenerator.Key, reportGenerator.Value, reportGenerationTime);
                        ApplicationInsightsHelper.TrackReportProcessed(reportGenerator.Key.ReportName + " report");
                    }

                    await RebuildPackageReports(destinationContainer, reportGenerationTime);
                    await CleanInactiveRecentPopularityDetailByPackageReports(destinationContainer, reportGenerationTime);
                }
                else
                {
                    // generate only the specific report
                    var reportBuilder = new ReportBuilder(_reportName);
                    var reportDataCollector = new ReportDataCollector(_storedProcedures[_reportName], _statisticsDatabase);

                    await ProcessReport(destinationContainer, reportBuilder, reportDataCollector, reportGenerationTime);
                }

                _logger.LogInformation("Generated reports from {DataSource}/{InitialCatalog} and saving to {AccountName}/{Container}", _statisticsDatabase.DataSource, _statisticsDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

                // totals reports
                var stopwatch = Stopwatch.StartNew();

                // build downloads.v1.json
                var targets = new List<StorageContainerTarget>();
                targets.Add(new StorageContainerTarget(_cloudStorageAccount, _statisticsContainerName));
                foreach (var dataContainerName in _dataContainerNames)
                {
                    targets.Add(new StorageContainerTarget(_dataStorageAccount, dataContainerName));
                }
                var downloadCountReport = new DownloadCountReport(targets, _statisticsDatabase, _galleryDatabase);
                await downloadCountReport.Run();

                stopwatch.Stop();
                ApplicationInsightsHelper.TrackMetric(DownloadCountReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                ApplicationInsightsHelper.TrackReportProcessed(DownloadCountReport.ReportName);
                stopwatch.Restart();

                // build stats-totals.json
                var galleryTotalsReport = new GalleryTotalsReport(_cloudStorageAccount, _statisticsContainerName, _statisticsDatabase, _galleryDatabase);
                await galleryTotalsReport.Run();

                stopwatch.Stop();
                ApplicationInsightsHelper.TrackMetric(GalleryTotalsReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                ApplicationInsightsHelper.TrackReportProcessed(GalleryTotalsReport.ReportName);


                // build tools.v1.json
                var toolsReport = new DownloadsPerToolVersionReport(_cloudStorageAccount, _statisticsContainerName, _statisticsDatabase, _galleryDatabase);
                await toolsReport.Run();

                stopwatch.Stop();
                ApplicationInsightsHelper.TrackMetric(DownloadsPerToolVersionReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                ApplicationInsightsHelper.TrackReportProcessed(DownloadsPerToolVersionReport.ReportName);
                stopwatch.Restart();

                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError("Job run failed! {Exception}", exception);

                return false;
            }
        }

        private static async Task ProcessReport(CloudBlobContainer destinationContainer, ReportBuilder reportBuilder, ReportDataCollector reportDataCollector, DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            var dataTable = await reportDataCollector.CollectAsync(reportGenerationTime, parameters);
            if (dataTable.Rows.Count == 0)
            {
                return;
            }

            var json = reportBuilder.CreateReport(dataTable);

            var reportWriter = new ReportWriter(destinationContainer);
            await reportWriter.WriteReport(reportBuilder.ReportArtifactName, json);
        }

        private async Task RebuildPackageReports(CloudBlobContainer destinationContainer, DateTime reportGenerationTime)
        {
            var dirtyPackageIds = await ReportDataCollector.GetDirtyPackageIds(_statisticsDatabase, reportGenerationTime);

            if (!dirtyPackageIds.Any())
                return;

            // first process the top 100 packages
            var top100 = dirtyPackageIds.Take(100);
            var reportDataCollector = new ReportDataCollector(_storedProceduresPerPackageId[ReportNames.RecentPopularityDetailByPackageId], _statisticsDatabase);
            var top100Task = Parallel.ForEach(top100, new ParallelOptions { MaxDegreeOfParallelism = 4 }, dirtyPackageId =>
            {
                var packageId = dirtyPackageId.PackageId.ToLowerInvariant();
                var reportBuilder = new RecentPopularityDetailByPackageReportBuilder(ReportNames.RecentPopularityDetailByPackageId, "recentpopularity/" + _recentPopularityDetailByPackageReportBaseName + packageId);

                ProcessReport(destinationContainer, reportBuilder, reportDataCollector, reportGenerationTime, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
                ApplicationInsightsHelper.TrackReportProcessed(reportBuilder.ReportName + " report", packageId);
            });

            // once top 100 is processed, continue with the rest
            if (top100Task.IsCompleted)
            {
                var excludingTop100 = dirtyPackageIds.Skip(100);

                top100Task = Parallel.ForEach(excludingTop100, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    dirtyPackageId =>
                    {
                        // generate all reports
                        var reportGenerators = new Dictionary<ReportBuilder, ReportDataCollector>
                        {
                            {
                                new RecentPopularityDetailByPackageReportBuilder(
                                    ReportNames.RecentPopularityDetailByPackageId,
                                    "recentpopularity/" + _recentPopularityDetailByPackageReportBaseName +
                                    dirtyPackageId.PackageId.ToLowerInvariant()),
                                new ReportDataCollector(
                                    _storedProceduresPerPackageId[ReportNames.RecentPopularityDetailByPackageId],
                                    _statisticsDatabase)
                            }
                        };

                        foreach (var reportGenerator in reportGenerators)
                        {
                            ProcessReport(destinationContainer, reportGenerator.Key, reportGenerator.Value,
                                reportGenerationTime, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
                            ApplicationInsightsHelper.TrackReportProcessed(reportGenerator.Key.ReportName + " report",
                                dirtyPackageId.PackageId.ToLowerInvariant());
                        }
                    });

                if (top100Task.IsCompleted)
                {
                    var runToCursor = dirtyPackageIds.First().RunToCuror;
                    await ReportDataCollector.UpdateDirtyPackageIdCursor(_statisticsDatabase, runToCursor);
                }
            }
        }

        private async Task CleanInactiveRecentPopularityDetailByPackageReports(CloudBlobContainer destinationContainer, DateTime reportGenerationTime)
        {
            _logger.LogDebug("Getting list of inactive packages.");
            var packageIds = await ReportDataCollector.ListInactivePackageIdReports(_statisticsDatabase, reportGenerationTime);
            _logger.LogInformation("Found {InactivePackageCount} inactive packages.", packageIds.Count);

            // Collect the list of reports
            var subContainer = "recentpopularity/";
            _logger.LogDebug("Collecting list of package detail reports");
            var reports = destinationContainer.ListBlobs(subContainer + _recentPopularityDetailByPackageReportBaseName)
                    .OfType<CloudBlockBlob>()
                    .Select(b => b.Name);

            var reportSet = new HashSet<string>(reports);
            _logger.LogInformation("Collected {PackageDetailReportCount} package detail reports", reportSet.Count);

            Parallel.ForEach(packageIds, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async id =>
             {
                 string reportName = _recentPopularityDetailByPackageReportBaseName + id;
                 string blobName = subContainer + reportName + ".json";
                 if (reportSet.Contains(blobName))
                 {
                     var blob = destinationContainer.GetBlockBlobReference(blobName);
                     _logger.LogDebug("{ReportName}: Deleting empty report from {BlobUri}", reportName, blob.Uri.AbsoluteUri);

                     await blob.DeleteIfExistsAsync();

                     _logger.LogInformation("{ReportName}: Deleted empty report from {BlobUri}", reportName, blob.Uri.AbsoluteUri);
                 }
             });
        }

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount, string parameterName)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException($"Job parameter {parameterName} is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }

            throw new ArgumentException($"Job parameter {parameterName} is invalid.");
        }

        private static string ValidateAzureContainerName(string containerName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException($"Job parameter {parameterName} is not defined.");
            }

            return containerName;
        }

        private static string ValidateReportName(string reportName)
        {
            if (string.IsNullOrWhiteSpace(reportName))
            {
                return null;
            }

            if (!_storedProcedures.ContainsKey(reportName.ToLowerInvariant()))
            {
                throw new ArgumentException("Job parameter ReportName contains unknown report name.");
            }

            return reportName;
        }

        private static class ReportNames
        {
            public const string NuGetClientVersion = "nugetclientversion";
            public const string Last6Weeks = "last6weeks";
            public const string RecentPopularity = "recentpopularity";
            public const string RecentPopularityDetail = "recentpopularitydetail";
            public const string RecentPopularityDetailByPackageId = "recentpopularitydetailbypackageid";
        }
    }
}