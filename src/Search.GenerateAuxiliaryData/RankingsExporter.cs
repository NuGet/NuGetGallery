// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace Search.GenerateAuxiliaryData
{
    // Public only to facilitate testing.
    public sealed class RankingsExporter : SqlExporter
    {
        private const string _rankingCountParameterName = "@RankingCount";
        private const int _rankingCount = 250;
        private const string _colPackageId = "PackageId";
        private readonly string _rankingsTotalScript;

        public RankingsExporter(
            ILogger<SqlExporter> logger,
            Func<Task<SqlConnection>> openSqlConnectionAsync,
            CloudBlobContainer defaultDestinationContainer,
            string defaultRankingsScript,
            string defaultName,
            TimeSpan commandTimeout)
            : base(logger, openSqlConnectionAsync, defaultDestinationContainer, defaultName, commandTimeout)
        {
            _rankingsTotalScript = defaultRankingsScript;
        }

        protected override JContainer GetResultOfQuery(SqlConnection connection)
        {
            var rankingsTotalCommand = GetEmbeddedSqlCommand(connection, _rankingsTotalScript);
            rankingsTotalCommand.Parameters.AddWithValue(_rankingCountParameterName, _rankingCount);

            return GetRankings(rankingsTotalCommand.ExecuteReader());
        }

        public JObject GetRankings(IDataReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var result = new JObject();
            var rankingsTotal = SqlDataReaderToJArray(reader, _colPackageId);

            result.Add("Rank", rankingsTotal);

            return result;
        }

        private static JArray SqlDataReaderToJArray(IDataReader reader, string column)
        {
            var colNames = GetColMappingFromSqlDataReader(reader);
            var result = new JArray();

            while (reader.Read())
            {
                result.Add(reader.GetString(colNames[column]));
            }

            reader.Close();

            return result;
        }
    }
}