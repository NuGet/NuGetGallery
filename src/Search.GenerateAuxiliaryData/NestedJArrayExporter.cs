// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace Search.GenerateAuxiliaryData
{
    class NestedJArrayExporter
        : SqlExporter
    {
        public string Col0 { get; }
        public string Col1 { get; }
        public string SqlScript { get; }

        public NestedJArrayExporter(
            ILogger<NestedJArrayExporter> logger,
            Func<Task<SqlConnection>> openSqlConnectionAsync,
            CloudBlobContainer defaultDestinationContainer,
            string defaultSqlScript,
            string defaultName,
            string defaultCol0,
            string defaultCol1,
            TimeSpan commandTimeout)
            : base(logger, openSqlConnectionAsync, defaultDestinationContainer, defaultName, commandTimeout)
        {
            Col0 = defaultCol0;
            Col1 = defaultCol1;
            SqlScript = defaultSqlScript;
        }

        protected override JContainer GetResultOfQuery(SqlConnection connection)
        {
            var command = GetEmbeddedSqlCommand(connection, SqlScript);

            return SqlDataReaderToNestedJArrays(command.ExecuteReader(), Col0, Col1);
        }

        private static JArray SqlDataReaderToNestedJArrays(SqlDataReader reader, string col0, string col1)
        {
            var colNames = GetColMappingFromSqlDataReader(reader);

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

            return MakeNestedJArray(parent);
        }

        private static JArray MakeNestedJArray(IDictionary<string, HashSet<string>> data)
        {
            var result = new JArray();
            foreach (var entry in data)
            {
                result.Add(new JArray(entry.Key, new JArray(entry.Value.ToArray())));
            }

            return result;
        }
    }
}
