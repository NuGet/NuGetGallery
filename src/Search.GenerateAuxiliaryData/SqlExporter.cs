// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Sql;

namespace Search.GenerateAuxiliaryData
{
    // Public only to facilitate testing.
    public abstract class SqlExporter : Exporter
    {
        private static Assembly _executingAssembly = Assembly.GetExecutingAssembly();
        private static string _assemblyName = _executingAssembly.GetName().Name;
        
        public ISqlConnectionFactory ConnectionFactory { get; }

        public SqlExporter(ILogger<SqlExporter> logger, ISqlConnectionFactory connectionFactory, CloudBlobContainer defaultDestinationContainer, string defaultName)
            : base(logger, defaultDestinationContainer, defaultName)
        {
            _logger = logger;
            ConnectionFactory = connectionFactory;
        }

        protected static string GetEmbeddedSqlScript(string resourceName)
        {
            var stream = _executingAssembly.GetManifestResourceStream(_assemblyName + "." + resourceName);
            return new StreamReader(stream).ReadToEnd();
        }

        public override async Task ExportAsync()
        {
            _logger.LogInformation("Generating {ReportName} report from {DataSource}/{InitialCatalog}.",
                _name, ConnectionFactory.DataSource, ConnectionFactory.InitialCatalog);

            JContainer result;
            using (var connection = await ConnectionFactory.CreateAsync())
            {
                result = GetResultOfQuery(connection);
            }

            await WriteToBlobAsync(_logger, _destinationContainer, result.ToString(Formatting.None), _name);
        }

        protected abstract JContainer GetResultOfQuery(SqlConnection connection);

        protected static Dictionary<string, int> GetColMappingFromSqlDataReader(IDataReader reader)
        {
            var colNames = new Dictionary<string, int>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                colNames[reader.GetName(i)] = i;
            }
            return colNames;
        }

        private static async Task WriteToBlobAsync(ILogger<Exporter> logger, CloudBlobContainer container, string content, string name)
        {
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(name);
            logger.LogInformation("Writing report to {0}", blob.Uri.AbsoluteUri);

            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(content);

            logger.LogInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);
        }
    }
}
