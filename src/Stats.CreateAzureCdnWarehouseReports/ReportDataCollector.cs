// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportDataCollector
    {
        private int _commandTimeoutSeconds;
        private readonly ApplicationInsightsHelper _applicationInsightsHelper;
        private readonly string _procedureName;
        private readonly Func<Task<SqlConnection>> _openGallerySqlConnectionAsync;

        private ILogger<ReportDataCollector> _logger;

        public ReportDataCollector(
            ILogger<ReportDataCollector> logger,
            string procedureName,
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            int commandTimeoutSeconds,
            ApplicationInsightsHelper applicationInsightsHelper)
        {
            _logger = logger;
            _procedureName = procedureName;
            _openGallerySqlConnectionAsync = openGallerySqlConnectionAsync;
            _commandTimeoutSeconds = commandTimeoutSeconds;
            _applicationInsightsHelper = applicationInsightsHelper ?? throw new ArgumentNullException(nameof(applicationInsightsHelper));
        }

        public async Task<DataTable> CollectAsync(DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            _logger.LogInformation("{ProcedureName}: Collecting data", _procedureName);

            DataTable table = null;

            // Get the data
            await WithRetry(async () => table = await ExecuteSql(reportGenerationTime, parameters), _logger, _applicationInsightsHelper);

            Debug.Assert(table != null);
            _logger.LogInformation("{ProcedureName}: Collected {RowsCount} rows", _procedureName, table.Rows.Count);
            return table;
        }

        public static async Task<IReadOnlyCollection<DirtyPackageId>> GetDirtyPackageIds(
            ILogger logger,
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            DateTime reportGenerationTime,
            int commandTimeout,
            ApplicationInsightsHelper applicationInsightsHelper)
        {
            logger.LogInformation("Getting list of dirty packages IDs.");

            IReadOnlyCollection<DirtyPackageId> packageIds = new List<DirtyPackageId>();

            // Get the data
            await WithRetry(
                async () => packageIds = await GetDirtyPackageIdsFromWarehouse(openGallerySqlConnectionAsync, reportGenerationTime, commandTimeout),
                logger,
                applicationInsightsHelper);

            logger.LogInformation("Found {DirtyPackagesCount} dirty packages to update.", packageIds.Count);

            return packageIds;
        }

        public static async Task<IReadOnlyCollection<string>> ListInactivePackageIdReports(
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            DateTime reportGenerationTime,
            int commandTimeout)
        {
            using (var connection = await openGallerySqlConnectionAsync())
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

        private static async Task WithRetry(Func<Task> action, ILogger logger, ApplicationInsightsHelper applicationInsightsHelper)
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

                if (caught is SqlException sqlException && sqlException.InnerException is Win32Exception win32Exception)
                {
                    logger.LogError("SqlException with inner Win32Exception, native error code: {NativeErrorCode}", win32Exception.NativeErrorCode);

                    // Native error code reference:
                    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/18d8fbe8-a967-4f1c-ae50-99ca8e491d2d
                    if (win32Exception.NativeErrorCode == 0x00000102) // WAIT_TIMEOUT
                    {
                        applicationInsightsHelper.TrackMetric("Stats.SqlQueryTimeout", 1);
                    }
                }

                await Task.Delay(20 * 1000);
            }
        }

        private async Task<DataTable> ExecuteSql(DateTime reportGenerationTime, params Tuple<string, int, string>[] parameters)
        {
            using (var connection = await _openGallerySqlConnectionAsync())
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
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            DateTime reportGenerationTime,
            int commandTimeout)
        {
            using (var connection = await openGallerySqlConnectionAsync())
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
            Func<Task<SqlConnection>> openGallerySqlConnectionAsync,
            DateTime runToCursor,
            int commandTimeout)
        {
            using (var connection = await openGallerySqlConnectionAsync())
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