// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;

namespace Search.GenerateAuxiliaryData
{
    internal class Job
        : JobBase
    {
        private const int _defaultRankingCount = 250;
        private const string _defaultContainerName = "ng-search-data";

        private SqlExportArguments _curatedFeedArgs;
        private SqlExportArguments _ownersArgs;
        private SqlConnectionStringBuilder _warehouseConnection;
        private int? _rankingCount;
        private CloudStorageAccount _destinationStorageAccount;
        private CloudBlobContainer _destinationContainer;
        private string _destinationContainerName;
        private string _curatedFeedsSql;
        private string _ownersSql;
        private string _overallRankingsScript;
        private string _rankingByProjectTypeScript;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = executingAssembly.GetName().Name;

            _curatedFeedsSql = GetEmbeddedSqlScript(executingAssembly, assemblyName, "SqlScripts.CuratedFeed.sql");
            _ownersSql = GetEmbeddedSqlScript(executingAssembly, assemblyName, "SqlScripts.Owners.sql");
            _overallRankingsScript = GetEmbeddedSqlScript(executingAssembly, assemblyName, "SqlScripts.SearchRanking_Overall.sql");
            _rankingByProjectTypeScript = GetEmbeddedSqlScript(executingAssembly, assemblyName, "SqlScripts.SearchRanking_ByProjectType.sql");

            _warehouseConnection = new SqlConnectionStringBuilder(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceDatabase));

            _destinationStorageAccount = CloudStorageAccount.Parse(
                JobConfigurationManager.GetArgument(jobArgsDictionary,
                JobArgumentNames.TargetStorageAccount, EnvironmentVariableKeys.StoragePrimary));

            _destinationContainerName =
                JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName)
                ?? _defaultContainerName;

            _destinationContainer = _destinationStorageAccount.CreateCloudBlobClient().GetContainerReference(_destinationContainerName);

            var rankingCountString = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.RankingCount);
            _rankingCount = string.IsNullOrEmpty(rankingCountString) ? _defaultRankingCount : Convert.ToInt32(rankingCountString);

            _curatedFeedArgs = new SqlExportArguments(jobArgsDictionary, _destinationContainerName, "curatedfeeds.json");
            _ownersArgs = new SqlExportArguments(jobArgsDictionary, _destinationContainerName, "owners.json");

            return true;
        }

        public override async Task<bool> Run()
        {
            // curated feeds JSON
            var result = await RunSqlExportAsync(_curatedFeedArgs, _curatedFeedsSql, "FeedName", "Id");

            // owners JSON
            result &= await RunSqlExportAsync(_ownersArgs, _ownersSql, "Id", "UserName");

            // ranking JSON
            result &= await GenerateRankingsJsonAsync();

            return result;
        }

        private static string GetEmbeddedSqlScript(Assembly executingAssembly, string assemblyName, string resourceName)
        {
            var stream = executingAssembly.GetManifestResourceStream(assemblyName + "." + resourceName);
            return new StreamReader(stream).ReadToEnd();
        }

        private async Task<bool> GenerateRankingsJsonAsync()
        {
            var report = new JObject();

            using (var connection = await _warehouseConnection.ConnectTo())
            {
                // Gather overall rankings
                Trace.TraceInformation("Gathering overall rankings...");
                var searchRankingEntries = (
                    await connection.QueryWithRetryAsync<SearchRankingEntry>(
                        _overallRankingsScript,
                        new { RankingCount = _rankingCount },
                        commandTimeout: 120)
                        ).ToList();
                Trace.TraceInformation("Gathered {0} rows of data.", searchRankingEntries.Count);

                // Get project types
                Trace.TraceInformation("Getting Project Types...");
                var projectTypes = (await connection.QueryAsync<string>("SELECT ProjectTypes FROM Dimension_Project")).ToList();
                Trace.TraceInformation("Got {0} project types", projectTypes.Count);

                // Gather data by project type
                Trace.TraceInformation("Gathering Project Type Rankings...");
                report.Add("Rank", new JArray(searchRankingEntries.Select(e => e.PackageId)));

                var count = 0;
                foreach (var projectType in projectTypes)
                {
                    Trace.TraceInformation("Gathering Project Type Rankings for '{0}'...", projectType);

                    var sqlParams = new { RankingCount = _rankingCount, ProjectGuid = projectType };
                    var data = await connection.QueryWithRetryAsync<SearchRankingEntry>(_rankingByProjectTypeScript, sqlParams, commandTimeout: 120);
                    var projectTypeRankingData = new JArray(data.Select(e => e.PackageId));

                    report.Add(projectType, projectTypeRankingData);

                    Trace.TraceInformation("Gathered {0} rows of data for project type '{1}'.", projectTypeRankingData.Count, projectType);

                    count += projectTypeRankingData.Count;
                }

                Trace.TraceInformation("Gathered {0} rows of data for all project types.", count);
            }

            // Write the JSON blob
            await WriteToBlobAsync(_destinationContainer, report.ToString(Formatting.Indented), "rankings.v1.json");

            return true;
        }

        private static async Task<bool> RunSqlExportAsync(SqlExportArguments args, string sql, string col0, string col1)
        {
            Trace.TraceInformation("Generating Curated feed report from {0}.", TracableConnectionString(args.ConnectionString));

            JArray result;
            using (var connection = new SqlConnection(args.ConnectionString))
            {
                connection.Open();

                var command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;

                result = SqlDataReader2Json(command.ExecuteReader(), col0, col1);
            }

            await WriteToBlobAsync(args.DestinationContainer, result.ToString(Formatting.None), args.Name);

            return true;
        }

        private static JArray SqlDataReader2Json(SqlDataReader reader, string col0, string col1)
        {
            var colNames = new Dictionary<string, int>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                colNames[reader.GetName(i)] = i;
            }

            var parent = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                var parentColumn = reader.GetString(colNames[col0]);
                var childColumn = reader.GetString(colNames[col1]);

                HashSet<string> child;
                if (!parent.TryGetValue(parentColumn, out child))
                {
                    child = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    parent.Add(parentColumn, child);
                }

                child.Add(childColumn);
            }

            return MakeJArray(parent);
        }

        private static JArray MakeJArray(IDictionary<string, HashSet<string>> data)
        {
            var result = new JArray();
            foreach (var entry in data)
            {
                result.Add(new JArray(entry.Key, new JArray(entry.Value.ToArray())));
            }

            return result;
        }

        private static string TracableConnectionString(string connectionString)
        {
            var connStr = new SqlConnectionStringBuilder(connectionString);
            connStr.UserID = "########";
            connStr.Password = "########";
            return connStr.ToString();
        }

        public static async Task WriteToBlobAsync(CloudBlobContainer container, string content, string name)
        {
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(name);
            Trace.TraceInformation("Writing report to {0}", blob.Uri.AbsoluteUri);

            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(content);

            Trace.TraceInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);
        }
    }
}