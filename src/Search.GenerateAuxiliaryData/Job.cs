// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
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
        private const string ScriptRankingsProjectTypes = "SqlScripts.RankingsProjectTypes.sql";
        private const string ScriptRankingsDistinctProjectTypes = "SqlScripts.RankingsDistinctProjectTypes.sql";
        private const string OutputNameRankings = "rankings.v1.json";

        private List<SqlExporter> _sqlExportScriptsToRun;
        private CloudBlobContainer _destContainer;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
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

            _sqlExportScriptsToRun = new List<GenerateAuxiliaryData.SqlExporter> {
                new NestedJArrayExporter(packageDatabaseConnString, _destContainer, ScriptCuratedFeed, OutputNameCuratedFeed, Col0CuratedFeed, Col1CuratedFeed),
                new NestedJArrayExporter(packageDatabaseConnString, _destContainer, ScriptOwners, OutputNameOwners, Col0Owners, Col1Owners),
                new RankingsExporter(statisticsDatabaseConnString, _destContainer, ScriptRankingsTotal, ScriptRankingsProjectTypes, ScriptRankingsDistinctProjectTypes, OutputNameRankings)
            };

            return true;
        }

        public override async Task<bool> Run()
        {
            var result = true;

            foreach (SqlExporter exporter in _sqlExportScriptsToRun)
            {
                result &= await exporter.RunSqlExportAsync();
            }

            return result;
        }
    }
}