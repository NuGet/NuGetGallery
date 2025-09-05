// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stats.ImportAzureCdnStatistics;

namespace Stats.RefreshClientDimension
{
    static internal class Warehouse
    {
        private const int _defaultCommandTimeout = 1800; // 30 minutes max

        internal static async Task<IDictionary<string, Tuple<int, int>>> GetUnknownUserAgents(SqlConnection connection)
        {
            var results = new Dictionary<string, Tuple<int, int>>();
            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[GetUnknownUserAgents]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _defaultCommandTimeout;

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var userAgent = dataReader.GetString(0);
                    var userAgentId = dataReader.GetInt32(1);
                    results.Add(userAgent, new Tuple<int, int>(userAgentId, ClientDimension.Unknown.Id));
                }
            }

            return results;
        }

        internal static async Task<IDictionary<string, Tuple<int, int>>> GetLinkedUserAgents(SqlConnection connection, string targetClientName, string userAgentFilter)
        {
            if (string.IsNullOrWhiteSpace(targetClientName))
            {
                return new Dictionary<string, Tuple<int, int>>();
            }

            var results = new Dictionary<string, Tuple<int, int>>();
            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[GetLinkedUserAgents]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _defaultCommandTimeout;

            command.Parameters.AddWithValue("TargetClientName", targetClientName);

            if (!string.IsNullOrWhiteSpace(userAgentFilter))
            {
                command.Parameters.AddWithValue("UserAgentFilter", userAgentFilter);
            }

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var userAgent = dataReader.GetString(0);
                    var userAgentId = dataReader.GetInt32(1);
                    var clientDimensionId = dataReader.GetInt32(2);
                    results.Add(userAgent, new Tuple<int, int>(userAgentId, clientDimensionId));
                }
            }

            return results;
        }

        public static async Task<IDictionary<string, int>> EnsureClientDimensionsExist(SqlConnection connection, IDictionary<string, Tuple<int, ClientDimension>> recognizedUserAgents)
        {
            var results = new Dictionary<string, int>();

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureClientDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _defaultCommandTimeout;

            var parameterValue = ClientDimensionTableType.CreateDataTable(recognizedUserAgents.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item2));

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

        public static async Task<IDictionary<string, int>> EnsureUserAgentFactsExist(SqlConnection connection, IReadOnlyCollection<string> userAgents)
        {
            var results = new Dictionary<string, int>();
            if (!userAgents.Any())
            {
                return results;
            }

            var parameterValue = UserAgentFactTableType.CreateDataTable(userAgents.Distinct().ToList());

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

        public static async Task PatchClientDimension(ILogger logger, SqlConnection connection, IReadOnlyCollection<UserAgentToClientDimensionLink> links)
        {
            var count = links.Count;
            var i = 0;

            foreach (var link in links)
            {
                i++;
                
                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[PatchClientDimensionForUserAgent]";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _defaultCommandTimeout;

                command.Parameters.AddWithValue("CurrentClientDimensionId", link.CurrentClientDimensionId);
                command.Parameters.AddWithValue("NewClientDimensionId", link.NewClientDimensionId);
                command.Parameters.AddWithValue("UserAgentId", link.UserAgentId);

                logger.LogInformation("[{LinkIndex}/{TotalLinksCount}]: User Agent '{UserAgent}', User Agent Id '{UserAgentId}', Old Client Id '{OldClientId}', New Client Id '{NewClientId}'", 
                    i, count, link.UserAgent, link.UserAgentId, link.CurrentClientDimensionId, link.NewClientDimensionId);

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}