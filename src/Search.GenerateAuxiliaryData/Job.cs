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
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;

namespace Search.GenerateAuxiliaryData
{
    internal class Job
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
            var secretInjector = (ISecretInjector)serviceContainer.GetService(typeof(ISecretInjector));

            var packageDbConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase);
            var packageDbConnectionFactory = new AzureSqlConnectionFactory(packageDbConnectionString, secretInjector);

            var statisticsDbConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
            var statisticsDbConnectionFactory = new AzureSqlConnectionFactory(statisticsDbConnectionString, secretInjector);

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
                new VerifiedPackagesExporter(LoggerFactory.CreateLogger<VerifiedPackagesExporter>(), packageDbConnectionFactory, _destContainer, ScriptVerifiedPackages, OutputNameVerifiedPackages),
                new NestedJArrayExporter(LoggerFactory.CreateLogger<NestedJArrayExporter>(), packageDbConnectionFactory, _destContainer, ScriptCuratedFeed, OutputNameCuratedFeed, Col0CuratedFeed, Col1CuratedFeed),
                new NestedJArrayExporter(LoggerFactory.CreateLogger<NestedJArrayExporter>(), packageDbConnectionFactory, _destContainer, ScriptOwners, OutputNameOwners, Col0Owners, Col1Owners),
                new RankingsExporter(LoggerFactory.CreateLogger<RankingsExporter>(), statisticsDbConnectionFactory, _destContainer, ScriptRankingsTotal, OutputNameRankings),
                new BlobStorageExporter(LoggerFactory.CreateLogger<BlobStorageExporter>(), _statisticsContainer, StatisticsReportName, _destContainer, StatisticsReportName)
            };
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