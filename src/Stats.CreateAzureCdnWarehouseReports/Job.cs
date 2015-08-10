// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class Job
        : JobBase
    {
        private const string _packageReportDetailBaseName = "recentpopularitydetail_";
        private CloudStorageAccount _cloudStorageAccount;
        private string _cloudStorageContainerName;
        private SqlConnectionStringBuilder _sourceDatabase;
        private string _reportName;

        private static readonly IDictionary<string, string> _storedProcedures = new Dictionary<string, string>
        {
            {ReportNames.NuGetClientVersion, "[dbo].[DownloadReportNuGetClientVersion]" },
            {ReportNames.Last6Months, "[dbo].[DownloadReportLast6Months]" },
            {ReportNames.RecentPopularity, "[dbo].[DownloadReportRecentPopularity]" },
            {ReportNames.RecentPopularityDetail, "[dbo].[DownloadReportRecentPopularityDetail]" },
        };

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var cloudStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString);

                _sourceDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
                _cloudStorageContainerName = ValidateAzureContainerName(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName));
                _reportName = ValidateReportName(JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.WarehouseReportName));

                return true;
            }
            catch (Exception exception)
            {
                ApplicationInsights.TrackException(exception);
                Trace.TraceError(exception.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            try
            {

                var destinationContainer = _cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(_cloudStorageContainerName);
                Trace.TraceInformation("Generating reports from {0}/{1} and saving to {2}/{3}", _sourceDatabase.DataSource, _sourceDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

                if (string.IsNullOrEmpty(_reportName))
                {
                    // generate all reports
                    var reportGenerators = new Dictionary<ReportBuilder, ReportDataCollector>
                    {
                        { new ReportBuilder(ReportNames.NuGetClientVersion), new ReportDataCollector(_storedProcedures[ReportNames.NuGetClientVersion], _sourceDatabase) },
                        { new ReportBuilder(ReportNames.Last6Months), new ReportDataCollector(_storedProcedures[ReportNames.Last6Months], _sourceDatabase) },
                        { new ReportBuilder(ReportNames.RecentPopularity), new ReportDataCollector(_storedProcedures[ReportNames.RecentPopularity], _sourceDatabase) },
                        { new ReportBuilder(ReportNames.RecentPopularityDetail), new ReportDataCollector(_storedProcedures[ReportNames.RecentPopularityDetail], _sourceDatabase) }
                    };

                    foreach (var reportGenerator in reportGenerators)
                    {
                        await ProcessReport(destinationContainer, reportGenerator.Key, reportGenerator.Value);
                    }

                    await RebuildPackageReports(destinationContainer);
                    await CleanInactivePackageReports(destinationContainer);
                }
                else
                {
                    // generate only the specific report
                    var reportBuilder = new ReportBuilder(_reportName);
                    var reportDataCollector = new ReportDataCollector(_storedProcedures[_reportName], _sourceDatabase);

                    await ProcessReport(destinationContainer, reportBuilder, reportDataCollector);
                }

                Trace.TraceInformation("Generated reports from {0}/{1} and saving to {2}/{3}", _sourceDatabase.DataSource, _sourceDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, destinationContainer.Name);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                ApplicationInsights.TrackException(exception);
                return false;
            }
        }

        private static async Task ProcessReport(CloudBlobContainer destinationContainer, ReportBuilder reportBuilder, ReportDataCollector reportDataCollector, params Tuple<string, int, string>[] parameters)
        {
            var dataTable = await reportDataCollector.CollectAsync(parameters);
            var json = reportBuilder.CreateReport(dataTable);

            var reportWriter = new ReportWriter(destinationContainer);
            await reportWriter.WriteReport(reportBuilder.ReportName, json);
        }

        private async Task RebuildPackageReports(CloudBlobContainer destinationContainer)
        {
            var dirtyPackageIds = await ReportDataCollector.GetDirtyPackageIds(_sourceDatabase);
            if (!dirtyPackageIds.Any())
                return;

            var result = Parallel.ForEach(dirtyPackageIds, new ParallelOptions { MaxDegreeOfParallelism = 8 }, dirtyPackageId =>
            {
                var reportName = _packageReportDetailBaseName + dirtyPackageId.PackageId.ToLowerInvariant();
                var reportBuilder = new RecentPopularityDetailByPackageReportBuilder(reportName);
                var reportDataCollector = new ReportDataCollector("[dbo].[DownloadReportRecentPopularityDetailByPackage]", _sourceDatabase);

                ProcessReport(destinationContainer, reportBuilder, reportDataCollector, Tuple.Create("@PackageId", 128, dirtyPackageId.PackageId)).Wait();
            });

            if (result.IsCompleted)
            {
                var runToCursor = dirtyPackageIds.First().RunToCuror;
                await ReportDataCollector.UpdateDirtyPackageIdCursor(_sourceDatabase, runToCursor);
            }
        }


        private async Task CleanInactivePackageReports(CloudBlobContainer destinationContainer)
        {
            Trace.TraceInformation("Getting list of inactive packages.");
            IList<string> packageIds;
            using (var connection = await _sourceDatabase.ConnectTo())
            {
                var sql = "[dbo].[DownloadReportListInactive]";
                packageIds = (await connection.QueryAsync<string>(sql, CommandType.StoredProcedure)).ToList();
            }
            Trace.TraceInformation("Found {0} inactive packages.", packageIds.Count);

            // Collect the list of reports
            var subContainer = "recentpopularity/";
            Trace.TraceInformation("Collecting list of package detail reports");
            var reports = destinationContainer.ListBlobs(subContainer + _packageReportDetailBaseName)
                    .OfType<CloudBlockBlob>()
                    .Select(b => b.Name);

            var reportSet = new HashSet<string>(reports);
            Trace.TraceInformation("Collected {0} package detail reports", reportSet.Count);

            Parallel.ForEach(packageIds, new ParallelOptions { MaxDegreeOfParallelism = 8}, async id =>
            {
                string reportName = _packageReportDetailBaseName + id;
                string blobName = subContainer + reportName + ".json";
                if (reportSet.Contains(blobName))
                {
                    var blob = destinationContainer.GetBlockBlobReference(blobName);
                    Trace.TraceInformation("{0}: Deleting empty report from {1}", reportName, blob.Uri.AbsoluteUri);

                    await blob.DeleteIfExistsAsync();

                    Trace.TraceInformation("{0}: Deleted empty report from {1}", reportName, blob.Uri.AbsoluteUri);
                }
            });
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

        private static string ValidateAzureContainerName(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Job parameter for Azure Storage Container Name is not defined.");
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
            public const string Last6Months = "last6months";
            public const string RecentPopularity = "recentpopularity";
            public const string RecentPopularityDetail = "recentpopularitydetail";
        }
    }
}