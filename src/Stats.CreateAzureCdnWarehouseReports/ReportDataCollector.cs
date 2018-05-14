// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Sql;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportDataCollector
    {
        private int _commandTimeoutSeconds;
        private readonly string _procedureName;
        private readonly ISqlConnectionFactory _sourceDbConnectionFactory;

        private ILogger<ReportDataCollector> _logger;

        public ReportDataCollector(
            ILogger<ReportDataCollector> logger,
            string procedureName,
            ISqlConnectionFactory sourceDbConnectionFactory,
            int timeout)
        {
            _logger = logger;
            _procedureName = procedureName;
            _sourceDbConnectionFactory = sourceDbConnectionFactory;
            _commandTimeoutSeconds = timeout;
        }

        public async Task<DataTable> CollectAsync(DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            _logger.LogInformation("{ProcedureName}: Collecting data", _procedureName);

            DataTable table = null;

            // Get the data
            await WithRetry(async () => table = await ExecuteSql(reportGenerationTime, parameters), _logger);

            Debug.Assert(table != null);
            _logger.LogInformation("{ProcedureName}: Collected {RowsCount} rows", _procedureName, table.Rows.Count);
            return table;
        }

        public static async Task<IReadOnlyCollection<DirtyPackageId>> GetDirtyPackageIds(
            ILogger logger,
            ISqlConnectionFactory sourceDbConnectionFactory,
            DateTime reportGenerationTime,
            int commandTimeout)
        {
            logger.LogInformation("Getting list of dirty packages IDs.");

            IReadOnlyCollection<DirtyPackageId> packageIds = new List<DirtyPackageId>();

            // Get the data
            await WithRetry(async () => packageIds = await GetDirtyPackageIdsFromWarehouse(sourceDbConnectionFactory, reportGenerationTime, commandTimeout), logger);

            logger.LogInformation("Found {DirtyPackagesCount} dirty packages to update.", packageIds.Count);

            return packageIds;
        }

        public static async Task<IReadOnlyCollection<string>> ListInactivePackageIdReports(
            ISqlConnectionFactory sourceDbConnectionFactory,
            DateTime reportGenerationTime,
            int commandTimeout)
        {
            using (var connection = await sourceDbConnectionFactory.CreateAsync())
            {
                var command = new SqlCommand("[dbo].[DownloadReportListInactive]", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = commandTimeout;

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

        private static async Task WithRetry(Func<Task> action, ILogger logger)
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
                logger.LogError("SQL Invocation failed, retrying. {RemainingAttempts} attempts remaining. Exception: {Exception}", attempts, caught);
                await Task.Delay(20 * 1000);
            }
        }

        private async Task<DataTable> ExecuteSql(DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            using (var connection = await _sourceDbConnectionFactory.CreateAsync())
            {
                var command = new SqlCommand(_procedureName, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _commandTimeoutSeconds;

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

        private static async Task<IReadOnlyCollection<DirtyPackageId>> GetDirtyPackageIdsFromWarehouse(
            ISqlConnectionFactory sourceDbConnectionFactory,
            DateTime reportGenerationTime,
            int commandTimeout)
        {
            using (var connection = await sourceDbConnectionFactory.CreateAsync())
            {
                var command = new SqlCommand("[dbo].[GetDirtyPackageIds]", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = commandTimeout;

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

        public static async Task UpdateDirtyPackageIdCursor(
            ISqlConnectionFactory sourceDbConnectionFactory,
            DateTime runToCursor,
            int commandTimeout)
        {
            using (var connection = await sourceDbConnectionFactory.CreateAsync())
            {
                var command = new SqlCommand("[dbo].[UpdateDirtyPackageIdCursor]", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = commandTimeout;
                command.Parameters.Add("@Position", SqlDbType.DateTime).Value = runToCursor;

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}