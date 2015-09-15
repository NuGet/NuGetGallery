// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportDataCollector
    {
        private const int _commandTimeout = 1800; // 30 minutes max
        private readonly string _procedureName;
        private readonly SqlConnectionStringBuilder _sourceDatabase;

        public ReportDataCollector(string procedureName, SqlConnectionStringBuilder sourceDatabase)
        {
            _procedureName = procedureName;
            _sourceDatabase = sourceDatabase;
        }

        public async Task<DataTable> CollectAsync(DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            Trace.TraceInformation("{0}: Collecting data", _procedureName);

            DataTable table = null;

            // Get the data
            await WithRetry(async () => table = await ExecuteSql(reportGenerationTime, parameters));

            Debug.Assert(table != null);
            Trace.TraceInformation("{0}: Collected {1} rows", _procedureName, table.Rows.Count);
            return table;
        }

        public static async Task<IReadOnlyCollection<DirtyPackageId>> GetDirtyPackageIds(SqlConnectionStringBuilder sourceDatabase, DateTime reportGenerationTime)
        {
            Trace.TraceInformation("Getting list of dirty packages IDs.");

            IReadOnlyCollection<DirtyPackageId> packageIds = new List<DirtyPackageId>();

            // Get the data
            await WithRetry(async () => packageIds = await GetDirtyPackageIdsFromWarehouse(sourceDatabase, reportGenerationTime));

            Trace.TraceInformation("Found {0} dirty packages to update.", packageIds.Count);

            return packageIds;
        }

        public static async Task<IReadOnlyCollection<string>> ListInactivePackageIdReports(SqlConnectionStringBuilder sourceDatabase, DateTime reportGenerationTime)
        {
            using (var connection = await sourceDatabase.ConnectTo())
            {
                var command = new SqlCommand("[dbo].[DownloadReportListInactive]", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _commandTimeout;

                command.Parameters.Add("ReportGenerationTime", SqlDbType.DateTime).Value = reportGenerationTime;

                var packageIds = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        packageIds.Add(reader.GetString(0));
                    }
                }

                return packageIds;
            }
        }

        private static async Task WithRetry(Func<Task> action)
        {
            int attempts = 10;

            while (attempts-- > 0)
            {
                Exception caught;
                try
                {
                    await action();
                    break;
                }
                catch (Exception ex)
                {
                    if (attempts == 1)
                    {
                        throw;
                    }
                    else
                    {
                        caught = ex;
                    }
                }

                SqlConnection.ClearAllPools();
                Trace.TraceError("SQL Invocation failed, retrying. {0} attempts remaining. Exception: {1}", attempts, caught);
                await Task.Delay(20 * 1000);
            }
        }

        private async Task<DataTable> ExecuteSql(DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            using (var connection = await _sourceDatabase.ConnectTo())
            {
                var command = new SqlCommand(_procedureName, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _commandTimeout;

                command.Parameters.Add("ReportGenerationTime", SqlDbType.DateTime).Value = reportGenerationTime;

                foreach (Tuple<string, int, string> parameter in parameters)
                {
                    command.Parameters.Add(parameter.Item1, SqlDbType.NVarChar, parameter.Item2).Value = parameter.Item3;
                }

                var table = new DataTable();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    table.Load(reader);
                }

                return table;
            }
        }

        private static async Task<IReadOnlyCollection<DirtyPackageId>> GetDirtyPackageIdsFromWarehouse(SqlConnectionStringBuilder sourceDatabase, DateTime reportGenerationTime)
        {
            using (var connection = await sourceDatabase.ConnectTo())
            {
                var command = new SqlCommand("[dbo].[GetDirtyPackageIds]", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _commandTimeout;

                command.Parameters.Add("ReportGenerationTime", SqlDbType.DateTime).Value = reportGenerationTime;

                var packageIds = new List<DirtyPackageId>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        packageIds.Add(new DirtyPackageId(reader.GetString(0), reader.GetDateTime(1)));
                    }
                }

                return packageIds;
            }
        }

        public static async Task UpdateDirtyPackageIdCursor(SqlConnectionStringBuilder sourceDatabase, DateTime runToCursor)
        {
            using (var connection = await sourceDatabase.ConnectTo())
            {
                var command = new SqlCommand("[dbo].[UpdateDirtyPackageIdCursor]", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _commandTimeout;
                command.Parameters.Add("@Position", SqlDbType.DateTime).Value = runToCursor;

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}