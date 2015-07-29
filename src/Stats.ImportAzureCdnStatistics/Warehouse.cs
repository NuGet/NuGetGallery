// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    internal class Warehouse
    {
        internal static async Task InsertDownloadFactsAsync(DataTable facts, SqlConnection connection)
        {
            Trace.WriteLine("Inserting into temp table...");
            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.BatchSize = facts.Rows.Count;
                bulkCopy.DestinationTableName = facts.TableName;

                await bulkCopy.WriteToServerAsync(facts);
            }
            Trace.Write("  DONE");
        }

        internal static async Task<IReadOnlyCollection<PackageDimension>> RetrievePackageDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var packages = sourceData
                .Select(e => new PackageDimension(e.PackageId, e.PackageVersion))
                .Distinct()
                .ToList();

            var results = new List<PackageDimension>();
            if (!packages.Any())
            {
                return results;
            }

            var parameterValue = CreateDataTable(packages);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsurePackageDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.Parameters.AddWithValue("packages", parameterValue);
            parameter.SqlDbType = SqlDbType.Structured;
            parameter.TypeName = "[dbo].[PackageDimensionTableType]";

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var package = new PackageDimension(dataReader.GetString(1), dataReader.GetString(2));
                    package.Id = dataReader.GetInt32(0);

                    if (!results.Contains(package))
                        results.Add(package);
                }
            }

            return results;
        }

        internal static async Task<IReadOnlyCollection<DateDimension>> RetrieveDateDimensions(SqlConnection connection, DateTime min, DateTime max)
        {
            var results = new List<DateDimension>();

            var command = connection.CreateCommand();
            command.CommandText = SqlQueries.GetDateDimensions(min, max);
            command.CommandType = CommandType.Text;

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var result = new DateDimension();
                    result.Id = dataReader.GetInt32(0);
                    result.Date = dataReader.GetDateTime(1);

                    results.Add(result);
                }
            }

            return results;
        }

        internal static async Task<IReadOnlyCollection<TimeDimension>> RetrieveTimeDimensions(SqlConnection connection)
        {
            var results = new List<TimeDimension>();

            var command = connection.CreateCommand();
            command.CommandText = SqlQueries.GetAllTimeDimensions();
            command.CommandType = CommandType.Text;

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var result = new TimeDimension();
                    result.Id = dataReader.GetInt32(0);
                    result.HourOfDay = dataReader.GetInt32(1);

                    results.Add(result);
                }
            }

            return results;
        }

        internal static async Task<IDictionary<string, int>> RetrieveOperationDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var operations = sourceData
                .Where(e => !string.IsNullOrEmpty(e.Operation))
                .Select(e => e.Operation)
                .Distinct()
                .ToList();

            var results = new Dictionary<string, int>();
            if (!operations.Any())
            {
                return results;
            }

            var operationsParameter = string.Join(",", operations);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureOperationDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("operations", operationsParameter);

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    results.Add(dataReader.GetString(1), dataReader.GetInt32(0));
                }
            }

            return results;
        }

        internal static async Task<IDictionary<string, int>> RetrieveProjectTypeDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var projectTypes = sourceData
                .Where(e => !string.IsNullOrEmpty(e.ProjectGuids))
                .SelectMany(e => e.ProjectGuids.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                .Distinct()
                .ToList();

            var results = new Dictionary<string, int>();
            if (!projectTypes.Any())
            {
                return results;
            }

            var projectTypesParameter = string.Join(",", projectTypes);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureProjectTypeDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("projectTypes", projectTypesParameter);

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    results.Add(dataReader.GetString(1), dataReader.GetInt32(0));
                }
            }

            return results;
        }

        internal static async Task<IDictionary<string, int>> RetrieveClientDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var clientDimensions = sourceData
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent)
                .Select(e => e.First())
                .ToDictionary(e => e.UserAgent, ClientDimension.FromPackageStatistic);

            var results = new Dictionary<string, int>();
            if (!clientDimensions.Any())
            {
                return results;
            }

            var parameterValue = CreateDataTable(clientDimensions);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureClientDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.Parameters.AddWithValue("clients", parameterValue);
            parameter.SqlDbType = SqlDbType.Structured;
            parameter.TypeName = "[dbo].[ClientDimensionTableType]";

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    results.Add(dataReader.GetString(1), dataReader.GetInt32(0));
                }
            }

            return results;
        }

        internal static async Task<IDictionary<string, int>> RetrievePlatformDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var platformDimensions = sourceData
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent)
                .Select(e => e.First())
                .ToDictionary(e => e.UserAgent, PlatformDimension.FromPackageStatistic);

            var results = new Dictionary<string, int>();
            if (!platformDimensions.Any())
            {
                return results;
            }

            var parameterValue = CreateDataTable(platformDimensions);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsurePlatformDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.Parameters.AddWithValue("platforms", parameterValue);
            parameter.SqlDbType = SqlDbType.Structured;
            parameter.TypeName = "[dbo].[PlatformDimensionTableType]";

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    results.Add(dataReader.GetString(1), dataReader.GetInt32(0));
                }
            }

            return results;
        }

        private static DataTable CreateDataTable(IDictionary<string, PlatformDimension> platformDimensions)
        {
            var table = new DataTable();
            table.Columns.Add("UserAgent", typeof(string));
            table.Columns.Add("OSFamily", typeof(string));
            table.Columns.Add("Major", typeof(int));
            table.Columns.Add("Minor", typeof(int));
            table.Columns.Add("Patch", typeof(int));
            table.Columns.Add("PatchMinor", typeof(int));

            foreach (var platformDimension in platformDimensions)
            {
                var row = table.NewRow();
                row["UserAgent"] = platformDimension.Key;
                row["OSFamily"] = platformDimension.Value.OSFamily;
                row["Major"] = platformDimension.Value.Major;
                row["Minor"] = platformDimension.Value.Minor;
                row["Patch"] = platformDimension.Value.Patch;
                row["PatchMinor"] = platformDimension.Value.PatchMinor;

                table.Rows.Add(row);
            }
            return table;
        }

        private static DataTable CreateDataTable(Dictionary<string, ClientDimension> clientDimensions)
        {
            var table = new DataTable();
            table.Columns.Add("UserAgent", typeof(string));
            table.Columns.Add("ClientName", typeof(string));
            table.Columns.Add("Major", typeof(int));
            table.Columns.Add("Minor", typeof(int));
            table.Columns.Add("Patch", typeof(int));

            foreach (var clientDimension in clientDimensions)
            {
                var row = table.NewRow();
                row["UserAgent"] = clientDimension.Key;
                row["ClientName"] = clientDimension.Value.ClientName;
                row["Major"] = clientDimension.Value.Major;
                row["Minor"] = clientDimension.Value.Minor;
                row["Patch"] = clientDimension.Value.Patch;

                table.Rows.Add(row);
            }
            return table;
        }

        private static DataTable CreateDataTable(IReadOnlyCollection<PackageDimension> packageDimensions)
        {
            var table = new DataTable();
            table.Columns.Add("PackageId", typeof(string));
            table.Columns.Add("PackageVersion", typeof(string));

            foreach (var packageDimension in packageDimensions)
            {
                var row = table.NewRow();
                row["PackageId"] = packageDimension.PackageId;
                row["PackageVersion"] = packageDimension.PackageVersion;

                table.Rows.Add(row);
            }
            return table;
        }
    }
}