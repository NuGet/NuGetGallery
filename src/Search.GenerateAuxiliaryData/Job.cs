// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;

namespace Search.GenerateAuxiliaryData
{
    public class Job
        : JobBase
    {
        private const string DefaultContainerName = "ng-search-data";

        private const string ScriptCuratedFeed = "SqlScripts.CuratedFeed.sql";
        private const string OutputNameCuratedFeed = "curatedfeeds.json";
        private const string Col0CuratedFeed = "FeedName";
        private const string Col1CuratedFeed = "Id";

        private const string ScriptOwners = "SqlScripts.Owners.sql";
        private const string OutputNameOwners = "owners.json";
        private const string Col0Owners = "Id";
        private const string Col1Owners = "UserName";

        private const string ScriptRankingsTotal = "SqlScripts.Rankings.sql";
        private const string OutputNameRankings = "rankings.v1.json";

        private const string ScriptVerifiedPackages = "SqlScripts.VerifiedPackages.sql";
        private const string OutputNameVerifiedPackages = "verifiedPackages.json";

        private const string StatisticsReportName = "downloads.v1.json";

        private List<Exporter> _exportersToRun;
        private CloudBlobContainer _destContainer;
        private CloudBlobContainer _statisticsContainer;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            RegisterDatabase(serviceContainer, jobArgsDictionary, JobArgumentNames.PackageDatabase);
            RegisterDatabase(serviceContainer, jobArgsDictionary, JobArgumentNames.StatisticsDatabase);

            var statisticsStorageAccount = CloudStorageAccount.Parse(
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount));

            var statisticsReportsContainerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName);

            var destination = CloudStorageAccount.Parse(
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PrimaryDestination));

            var destinationContainerName =
                            JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName)
                            ?? DefaultContainerName;

            _destContainer = destination.CreateCloudBlobClient().GetContainerReference(destinationContainerName);
            _statisticsContainer = statisticsStorageAccount.CreateCloudBlobClient().GetContainerReference(statisticsReportsContainerName);

            _exportersToRun = new List<Exporter> {
                new VerifiedPackagesExporter(
                    OpenGallerySqlConnectionAsync,
                    LoggerFactory.CreateLogger<VerifiedPackagesExporter>(),
                    _destContainer, ScriptVerifiedPackages, OutputNameVerifiedPackages),

                new NestedJArrayExporter(
                    OpenGallerySqlConnectionAsync,
                    LoggerFactory.CreateLogger<NestedJArrayExporter>(),
                    _destContainer, ScriptCuratedFeed, OutputNameCuratedFeed, Col0CuratedFeed, Col1CuratedFeed),

                new NestedJArrayExporter(
                    OpenGallerySqlConnectionAsync,
                    LoggerFactory.CreateLogger<NestedJArrayExporter>(), 
                    _destContainer, ScriptOwners, OutputNameOwners, Col0Owners, Col1Owners),

                new RankingsExporter(
                    OpenStatisticsSqlConnectionAsync,
                    LoggerFactory.CreateLogger<RankingsExporter>(),
                    _destContainer, ScriptRankingsTotal, OutputNameRankings),

                new BlobStorageExporter(
                    LoggerFactory.CreateLogger<BlobStorageExporter>(),
                    _statisticsContainer, StatisticsReportName, _destContainer, StatisticsReportName)
            };
        }

        public Task<SqlConnection> OpenGallerySqlConnectionAsync()
        {
            return OpenSqlConnectionAsync(JobArgumentNames.PackageDatabase);
        }

        public Task<SqlConnection> OpenStatisticsSqlConnectionAsync()
        {
            return OpenSqlConnectionAsync(JobArgumentNames.StatisticsDatabase);
        }

        public override async Task Run()
        {
            var failedExporters = new List<string>();

            foreach (Exporter exporter in _exportersToRun)
            {
                try
                {
                    await exporter.ExportAsync();
                }
                catch (Exception e)
                {
                    var exporterName = exporter.GetType().Name;
                    Logger.LogError("SQL exporter '{ExporterName}' failed: {Exception}", exporterName, e);
                    failedExporters.Add(exporterName);
                }
            }
            
            if (failedExporters.Any())
            {
                throw new ExporterException($"{failedExporters.Count()} tasks failed: {string.Join(", ", failedExporters)}");
            }
        }
    }
}