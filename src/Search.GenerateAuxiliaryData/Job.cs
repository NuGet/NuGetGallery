// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;

namespace Search.GenerateAuxiliaryData
{
    internal class Job
        : JobBase
    {
        private const string _defaultContainerName = "ng-search-data";

        private const string _scriptCuratedFeed = "SqlScripts.CuratedFeed.sql";
        private const string _outputNameCuratedFeed = "curatedfeeds.json";
        private const string _col0CuratedFeed = "FeedName";
        private const string _col1CuratedFeed = "Id";

        private const string _scriptOwners = "SqlScripts.Owners.sql";
        private const string _outputNameOwners = "owners.json";
        private const string _col0Owners = "Id";
        private const string _col1Owners = "UserName";

        private const string _scriptRankingsTotal = "SqlScripts.Rankings.sql";
        private const string _scriptRankingsProjectTypes = "SqlScripts.RankingsProjectTypes.sql";
        private const string _scriptRankingsDistinctProjectTypes = "SqlScripts.RankingsDistinctProjectTypes.sql";
        private const string _outputNameRankings = "rankings.v1.json";

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
                            ?? _defaultContainerName;

            _destContainer = destination.CreateCloudBlobClient().GetContainerReference(destinationContainerName);

            _sqlExportScriptsToRun = new List<GenerateAuxiliaryData.SqlExporter> {
                new NestedJArrayExporter(packageDatabaseConnString, _destContainer, _scriptCuratedFeed, _outputNameCuratedFeed, _col0CuratedFeed, _col1CuratedFeed),
                new NestedJArrayExporter(packageDatabaseConnString, _destContainer, _scriptOwners, _outputNameOwners, _col0Owners, _col1Owners),
                new RankingsExporter(statisticsDatabaseConnString, _destContainer, _scriptRankingsTotal, _scriptRankingsProjectTypes, _scriptRankingsDistinctProjectTypes, _outputNameRankings)
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