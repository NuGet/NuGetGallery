// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Stats.ImportAzureCdnStatistics;

namespace Stats.RefreshClientDimension
{
    internal class Warehouse
    {
        private const int _defaultCommandTimeout = 1800; // 30 minutes max

        public static async Task<IEnumerable<string>> GetUnknownUserAgents(SqlConnection connection)
        {
            var results = new List<string>();
            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[GetUnknownUserAgents]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _defaultCommandTimeout;

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var userAgent = dataReader.GetString(0);
                    results.Add(userAgent);
                }
            }

            return results;
        }

        public static async Task<IDictionary<string, int>> EnsureClientDimensionsExist(SqlConnection connection, IDictionary<string, ClientDimension> recognizedUserAgents)
        {
            var results = new Dictionary<string, int>();

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureClientDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _defaultCommandTimeout;

            var parameterValue = ClientDimensionTableType.CreateDataTable(recognizedUserAgents);

            var parameter = command.Parameters.AddWithValue("clients", parameterValue);
            parameter.SqlDbType = SqlDbType.Structured;
            parameter.TypeName = "[dbo].[ClientDimensionTableType]";

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var clientDimensionId = dataReader.GetInt32(0);
                    var userAgent = dataReader.GetString(1);

                    results.Add(userAgent, clientDimensionId);
                }
            }

            return results;
        }

        public static async Task<IDictionary<string, int>> EnsureUserAgentFactsExist(SqlConnection connection, IDictionary<string, UserAgentFact> userAgentFacts)
        {
            var results = new Dictionary<string, int>();
            if (!userAgentFacts.Any())
            {
                return results;
            }

            var parameterValue = UserAgentFactTableType.CreateDataTable(userAgentFacts);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureUserAgentFactsExist]";
            command.CommandTimeout = _defaultCommandTimeout;
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.Parameters.AddWithValue("useragents", parameterValue);
            parameter.SqlDbType = SqlDbType.Structured;
            parameter.TypeName = "[dbo].[UserAgentFactTableType]";

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    results.Add(dataReader.GetString(1), dataReader.GetInt32(0));
                }
            }

            return results;
        }

        public static async Task PatchClientDimension(SqlConnection connection, IDictionary<string, ClientDimension> recognizedUserAgents, IDictionary<string, int> recognizedUserAgentsWithClientDimensionId, IDictionary<string, int> recognizedUserAgentsWithUserAgentId)
        {
            var count = recognizedUserAgentsWithClientDimensionId.Count;
            var i = 0;

            foreach (var kvp in recognizedUserAgentsWithClientDimensionId)
            {
                i++;

                var userAgent = kvp.Key;
                var clientDimensionId = kvp.Value;
                var userAgentId = DimensionId.Unknown;
                if (recognizedUserAgentsWithUserAgentId.Any() && recognizedUserAgentsWithUserAgentId.ContainsKey(userAgent))
                {
                    userAgentId = recognizedUserAgentsWithUserAgentId[userAgent];
                }

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[PatchClientDimensionForUserAgent]";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _defaultCommandTimeout;

                command.Parameters.AddWithValue("NewClientDimensionId", clientDimensionId);
                command.Parameters.AddWithValue("UserAgentId", userAgentId);

                Trace.WriteLine(string.Format("[{0}/{1}]: Client Id '{2}', User Agent '{3}', User Agent Id '{4}'", i, count, clientDimensionId, userAgent, userAgentId));

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}