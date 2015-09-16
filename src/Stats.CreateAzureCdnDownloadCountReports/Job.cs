// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;

namespace Stats.CreateAzureCdnDownloadCountReports
{
    public class Job
        : JobBase
    {
        private CloudStorageAccount _cloudStorageAccount;
        private SqlConnectionStringBuilder _statisticsDatabase;
        private SqlConnectionStringBuilder _galleryDatabase;
        private string _statisticsContainerName;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var statisticsDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
                _statisticsDatabase = new SqlConnectionStringBuilder(statisticsDatabaseConnectionString);

                var galleryDatabaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceDatabase);
                _galleryDatabase = new SqlConnectionStringBuilder(galleryDatabaseConnectionString);

                var cloudStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString);
                _statisticsContainerName = ValidateAzureContainerName(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName));

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                ApplicationInsights.TrackException(exception);
                return false;
            }
        }

        public override async Task<bool> Run()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // build downloads.v1.json
                var downloadCountReport = new DownloadCountReport(_cloudStorageAccount, _statisticsContainerName, _statisticsDatabase, _galleryDatabase);
                await downloadCountReport.Run();

                stopwatch.Stop();
                ApplicationInsights.TrackMetric(DownloadCountReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                ApplicationInsights.TrackReportProcessed(DownloadCountReport.ReportName);
                stopwatch.Restart();

                // build stats-totals.json
                var galleryTotalsReport = new GalleryTotalsReport(_cloudStorageAccount, _statisticsContainerName, _statisticsDatabase, _galleryDatabase);
                await galleryTotalsReport.Run();

                stopwatch.Stop();
                ApplicationInsights.TrackMetric(GalleryTotalsReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                ApplicationInsights.TrackReportProcessed(GalleryTotalsReport.ReportName);


                // build tools.v1.json
                var toolsReport = new DownloadsPerToolVersionReport(_cloudStorageAccount, _statisticsContainerName, _statisticsDatabase, _galleryDatabase);
                await toolsReport.Run();

                stopwatch.Stop();
                ApplicationInsights.TrackMetric(DownloadsPerToolVersionReport.ReportName + " Generation Time (ms)", stopwatch.ElapsedMilliseconds);
                ApplicationInsights.TrackReportProcessed(DownloadsPerToolVersionReport.ReportName);
                stopwatch.Restart();

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                ApplicationInsights.TrackException(exception);
                return false;
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

        private static string ValidateAzureContainerName(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Job parameter for Azure Storage Container Name is not defined.");
            }
            return containerName;
        }
    }
}