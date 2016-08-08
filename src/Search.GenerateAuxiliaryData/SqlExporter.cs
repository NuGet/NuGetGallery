// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Search.GenerateAuxiliaryData
{
    internal abstract class SqlExporter
    {
        private static Assembly _executingAssembly = Assembly.GetExecutingAssembly();
        private static string _assemblyName = _executingAssembly.GetName().Name;
        
        public string ConnectionString { get; }
        public CloudBlobContainer DestinationContainer { get; }
        public string Name { get; }

        public SqlExporter(string defaultConnectionString, CloudBlobContainer defaultDestinationContainer, string defaultName)
        {
            ConnectionString = defaultConnectionString;
            DestinationContainer = defaultDestinationContainer;
            Name = defaultName;
        }

        protected static string GetEmbeddedSqlScript(string resourceName)
        {
            var stream = _executingAssembly.GetManifestResourceStream(_assemblyName + "." + resourceName);
            return new StreamReader(stream).ReadToEnd();
        }

        public async Task<bool> RunSqlExportAsync()
        {
            Trace.TraceInformation("Generating {0} report from {1}.", Name, TracableConnectionString(ConnectionString));

            JContainer result;
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                
                result = GetResultOfQuery(connection);
            }

            await WriteToBlobAsync(DestinationContainer, result.ToString(Formatting.None), Name);

            return true;
        }

        protected abstract JContainer GetResultOfQuery(SqlConnection connection);

        protected static Dictionary<string, int> GetColMappingFromSqlDataReader(SqlDataReader reader)
        {
            var colNames = new Dictionary<string, int>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                colNames[reader.GetName(i)] = i;
            }
            return colNames;
        }

        private static string TracableConnectionString(string connectionString)
        {
            var connStr = new SqlConnectionStringBuilder(connectionString);
            connStr.UserID = "########";
            connStr.Password = "########";
            return connStr.ToString();
        }

        private static async Task WriteToBlobAsync(CloudBlobContainer container, string content, string name)
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
