// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Search.GenerateAuxiliaryData
{
    // Public only to facilitate testing.
    public abstract class SqlExporter : Exporter
    {
        private static Assembly _executingAssembly = Assembly.GetExecutingAssembly();
        private static string _assemblyName = _executingAssembly.GetName().Name;

        private Func<Task<SqlConnection>> OpenSqlConnectionAsync { get; }

        private readonly TimeSpan _commandTimeout;

        public SqlExporter(
            ILogger<SqlExporter> logger,
            Func<Task<SqlConnection>> openSqlConnectionAsync,
            CloudBlobContainer defaultDestinationContainer,
            string defaultName,
            TimeSpan commandTimeout)
            : base(logger, defaultDestinationContainer, defaultName)
        {
            _logger = logger;
            OpenSqlConnectionAsync = openSqlConnectionAsync;
            _commandTimeout = commandTimeout;
        }

        [SuppressMessage("Microsoft.Security", "CA2100", Justification = "Query string comes from embedded resource, not user input.")]
        protected SqlCommand GetEmbeddedSqlCommand(SqlConnection connection, string resourceName)
        {
            using (var reader = new StreamReader(_executingAssembly.GetManifestResourceStream(_assemblyName + "." + resourceName)))
            {
                var commandText = reader.ReadToEnd();

                return new SqlCommand(commandText, connection)
                {
                    CommandType = CommandType.Text,
                    CommandTimeout = (int)_commandTimeout.TotalSeconds,
                };
            }
        }

        public override async Task ExportAsync()
        {
            JContainer result;
            using (var connection = await OpenSqlConnectionAsync())
            {
                _logger.LogInformation("Generating {ReportName} report from {DataSource}/{InitialCatalog}.",
                    _name, connection.DataSource, connection.Database);

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
