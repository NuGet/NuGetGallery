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
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;

namespace Search.GenerateAuxiliaryData
{
    internal class Job
        : JobBase
    {
        private const string _defaultContainerName = "ng-search-data";

        private SqlExportArguments _curatedFeedArgs;
        private SqlExportArguments _ownersArgs;
        private string _destinationContainerName;
        private string _curatedFeedsSql;
        private string _ownersSql;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = executingAssembly.GetName().Name;

            _curatedFeedsSql = GetEmbeddedSqlScript(executingAssembly, assemblyName, "SqlScripts.CuratedFeed.sql");
            _ownersSql = GetEmbeddedSqlScript(executingAssembly, assemblyName, "SqlScripts.Owners.sql");

            _destinationContainerName =
                JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName)
                ?? _defaultContainerName;

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

            return result;
        }

        private static string GetEmbeddedSqlScript(Assembly executingAssembly, string assemblyName, string resourceName)
        {
            var stream = executingAssembly.GetManifestResourceStream(assemblyName + "." + resourceName);
            return new StreamReader(stream).ReadToEnd();
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