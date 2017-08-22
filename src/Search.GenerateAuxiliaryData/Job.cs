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

        private List<SqlExporter> _sqlExportScriptsToRun;
        private CloudBlobContainer _destContainer;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var packageDatabaseConnString = new SqlConnectionStringBuilder(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase)).ToString();

            var statisticsDatabaseConnString = new SqlConnectionStringBuilder(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase)).ToString();

            var destination = CloudStorageAccount.Parse(
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PrimaryDestination));

            var destinationContainerName =
                            JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName)
                            ?? DefaultContainerName;

            _destContainer = destination.CreateCloudBlobClient().GetContainerReference(destinationContainerName);

            _sqlExportScriptsToRun = new List<SqlExporter> {
                new NestedJArrayExporter(LoggerFactory.CreateLogger<NestedJArrayExporter>(), packageDatabaseConnString, _destContainer, ScriptCuratedFeed, OutputNameCuratedFeed, Col0CuratedFeed, Col1CuratedFeed),
                new NestedJArrayExporter(LoggerFactory.CreateLogger<NestedJArrayExporter>(), packageDatabaseConnString, _destContainer, ScriptOwners, OutputNameOwners, Col0Owners, Col1Owners),
                new RankingsExporter(LoggerFactory.CreateLogger<RankingsExporter>(), statisticsDatabaseConnString, _destContainer, ScriptRankingsTotal, OutputNameRankings)
            };
        }

        public override async Task Run()
        {
            var failedSqlExporters = new List<string>();

            foreach (SqlExporter exporter in _sqlExportScriptsToRun)
            {
                try
                {
                    await exporter.RunSqlExportAsync();
                }
                catch (Exception e)
                {
                    var exporterName = exporter.GetType().Name;
                    Logger.LogError("SQL exporter '{ExporterName}' failed: {Exception}", exporterName, e);
                    failedSqlExporters.Add(exporterName);
                }
            }
            
            if (failedSqlExporters.Any())
            {
                throw new SqlExporterException($"{failedSqlExporters.Count()} tasks failed: {string.Join(", ", failedSqlExporters)}");
            }
        }
    }
}