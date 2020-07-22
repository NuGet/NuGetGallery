// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Stats.ImportAzureCdnStatistics
{
    internal class Warehouse
        : IStatisticsWarehouse
    {
        private const int _defaultCommandTimeout = 3600; // 60 minutes max
        private const int _maxRetryCount = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);
        private readonly ILogger _logger;
        private readonly ApplicationInsightsHelper _applicationInsightsHelper;
        private readonly Func<Task<SqlConnection>> _openStatisticsSqlConnectionAsync;
        private readonly IDictionary<PackageDimension, PackageDimension> _cachedPackageDimensions = new Dictionary<PackageDimension, PackageDimension>();
        private readonly IList<ToolDimension> _cachedToolDimensions = new List<ToolDimension>();
        private readonly IDictionary<string, int> _cachedClientDimensions = new Dictionary<string, int>();
        private readonly IDictionary<string, int> _cachedPlatformDimensions = new Dictionary<string, int>();
        private readonly IDictionary<string, int> _cachedOperationDimensions = new Dictionary<string, int>();
        private readonly IDictionary<string, int> _cachedUserAgentFacts = new Dictionary<string, int>();
        private IReadOnlyCollection<TimeDimension> _times;

        public Warehouse(
            ILoggerFactory loggerFactory,
            Func<Task<SqlConnection>> openStatisticsSqlConnectionAsync,
            ApplicationInsightsHelper applicationInsightsHelper)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<Warehouse>();

            _openStatisticsSqlConnectionAsync = openStatisticsSqlConnectionAsync
                ?? throw new ArgumentNullException(nameof(openStatisticsSqlConnectionAsync));
            _applicationInsightsHelper = applicationInsightsHelper
                ?? throw new ArgumentNullException(nameof(applicationInsightsHelper));
        }

        public async Task InsertDownloadFactsAsync(DataTable downloadFactsDataTable, string logFileName)
        {
            _logger.LogDebug("Inserting into facts table...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await SqlRetryUtility.RetrySql(
                    () => RunInsertDownloadFactsQueryAsync(downloadFactsDataTable, logFileName));

                stopwatch.Stop();
                _applicationInsightsHelper.TrackMetric("Insert facts duration (ms)", stopwatch.ElapsedMilliseconds, logFileName);
            }
            catch (Exception exception)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                _logger.LogError("Failed to insert download facts for {LogFile}.", logFileName);

                _applicationInsightsHelper.TrackException(exception, logFileName);

                throw;
            }

            _logger.LogDebug("  DONE");
        }

        private async Task RunInsertDownloadFactsQueryAsync(DataTable downloadFactsDataTable, string logFileName)
        {
            using (var connection = await _openStatisticsSqlConnectionAsync())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
                {
                    BatchSize = 25000,
                    DestinationTableName = downloadFactsDataTable.TableName,
                    BulkCopyTimeout = _defaultCommandTimeout
                };

                // This avoids identity insert issues, as these are db-generated.
                foreach (DataColumn column in downloadFactsDataTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(downloadFactsDataTable);

                transaction.Commit();
            }
        }

        public async Task<DataTable> CreateAsync(IReadOnlyCollection<PackageStatistics> sourceData, string logFileName)
        {
            var stopwatch = Stopwatch.StartNew();

            // insert any new dimension data first
            if (_times == null)
            {
                // this call is only needed once in the lifetime of the service
                _times = await GetDimension("time", logFileName, connection => RetrieveTimeDimensions(connection));
            }

            var packagesTask = GetDimension("package", logFileName, connection => RetrievePackageDimensions(sourceData, connection));
            var operationsTask = GetDimension("operation", logFileName, connection => RetrieveOperationDimensions(sourceData, connection));
            var clientsTask = GetDimension("client", logFileName, connection => RetrieveClientDimensions(sourceData, connection));
            var platformsTask = GetDimension("platform", logFileName, connection => RetrievePlatformDimensions(sourceData, connection));
            var datesTask = GetDimension("date", logFileName, connection => RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered)));
            var userAgentsTask = GetDimension("useragent", logFileName, connection => RetrieveUserAgentFacts(sourceData, connection));
            var logFileNamesTask = GetDimension("logfilename", logFileName, connection => RetrieveLogFileNameFacts(logFileName, connection));

            await Task.WhenAll(operationsTask, clientsTask, platformsTask, datesTask, packagesTask, userAgentsTask, logFileNamesTask);

            var operations = operationsTask.Result;
            var clients = clientsTask.Result;
            var platforms = platformsTask.Result;
            var userAgents = userAgentsTask.Result;
            var logFileNames = logFileNamesTask.Result;

            var dates = datesTask.Result;
            var packages = packagesTask.Result;

            // create facts data rows by linking source data with dimensions
            var dataImporter = new DataImporter(_openStatisticsSqlConnectionAsync);
            var factsDataTable = await dataImporter.GetDataTableAsync(DataImporter.Table.Fact_Download);

            var knownOperationsAvailable = operations.Any();
            var knownClientsAvailable = clients.Any();
            var knownPlatformsAvailable = platforms.Any();
            var knownUserAgentsAvailable = userAgents.Any();

            int logFileNameId = DimensionId.Unknown;
            if (logFileNames.Any() && logFileNames.ContainsKey(logFileName))
            {
                logFileNameId = logFileNames[logFileName];
            }

            _logger.LogDebug("Creating facts...");
            var stopwatchCreatingFacts = Stopwatch.StartNew();
            foreach (var groupedByPackageId in sourceData.GroupBy(e => e.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var packagesForId = packages.Where(e => string.Equals(e.PackageId, groupedByPackageId.Key, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var groupedByPackageIdAndVersion in groupedByPackageId.GroupBy(e => e.PackageVersion, StringComparer.OrdinalIgnoreCase))
                {
                    var package = packagesForId.FirstOrDefault(e => string.Equals(e.PackageVersion, groupedByPackageIdAndVersion.Key, StringComparison.OrdinalIgnoreCase));
                    if (package == null)
                    {
                        // This package id and version could not be 100% accurately parsed from the CDN Request URL,
                        // likely due to weird package ID which could be interpreted as a version string.
                        // Track it in Application Insights.
                        _applicationInsightsHelper.TrackPackageNotFound(groupedByPackageId.Key, groupedByPackageIdAndVersion.Key, logFileName);

                        continue;
                    }

                    var packageId = package.Id;
                    var dimensionIdsDictionary = new Dictionary<(int, int, int, int, int, int), int>();
                    foreach (var element in groupedByPackageIdAndVersion)
                    {
                        // required dimensions
                        var dateId = dates.First(e => e.Date.Equals(element.EdgeServerTimeDelivered.Date)).Id;
                        var timeId = _times.First(e => e.HourOfDay == element.EdgeServerTimeDelivered.Hour).Id;

                        // dimensions that could be "(unknown)"
                        int operationId = DimensionId.Unknown;

                        if (knownOperationsAvailable && operations.ContainsKey(element.Operation))
                        {
                            operationId = operations[element.Operation];
                        }

                        int platformId = DimensionId.Unknown;
                        if (knownPlatformsAvailable && platforms.ContainsKey(element.UserAgent))
                        {
                            platformId = platforms[element.UserAgent];
                        }

                        int clientId = DimensionId.Unknown;
                        if (knownClientsAvailable && clients.ContainsKey(element.UserAgent))
                        {
                            clientId = clients[element.UserAgent];
                        }

                        int userAgentId = DimensionId.Unknown;
                        if (knownUserAgentsAvailable)
                        {
                            var trimmedUserAgent = UserAgentFact.TrimUserAgent(element.UserAgent);
                            if (userAgents.ContainsKey(trimmedUserAgent))
                            {
                                userAgentId = userAgents[trimmedUserAgent];
                            }
                        }

                        var dimensionIds = (dateId, timeId, operationId, platformId, clientId, userAgentId);
                        if (dimensionIdsDictionary.ContainsKey(dimensionIds))
                        {
                            dimensionIdsDictionary[dimensionIds] += 1;
                        }
                        else
                        {
                            dimensionIdsDictionary[dimensionIds] = 1;
                        }
                    }

                    foreach (var dimensionIds in dimensionIdsDictionary)
                    {
                        // create fact
                        var dataRow = factsDataTable.NewRow();

                        (int dateId, int timeId, int operationId, int platformId, int clientId, int userAgentId) key = dimensionIds.Key;
                        var downloadCount = dimensionIds.Value;

                        FillDataRow(dataRow, key.dateId, key.timeId, packageId, key.operationId, key.platformId, key.clientId, key.userAgentId, logFileNameId, downloadCount);
                        factsDataTable.Rows.Add(dataRow);

                        _logger.LogDebug("Inserted 1 row into factsDataTable, which counts for {DownloadCount} downloads, with the dimension Ids (" +
                            "dateId: {DateId}, timeId: {TimeId}, packageId: {PackageId}, operationId: {OperationId}, platformId: {PlatformId}, clientId: {ClientId}, " +
                            "userAgentId: {UserAgentId}, logFileNameId: {LogFileNameId}).", downloadCount, key.dateId, key.timeId, packageId, key.operationId,
                            key.platformId, key.clientId, key.userAgentId, logFileNameId);
                    }
                }
            }
            stopwatchCreatingFacts.Stop();
            stopwatch.Stop();
            _logger.LogDebug("  DONE (" + factsDataTable.Rows.Count + " facts, " + stopwatch.ElapsedMilliseconds + "ms)");
            _applicationInsightsHelper.TrackMetric("Facts creation time (ms)", stopwatchCreatingFacts.ElapsedMilliseconds, logFileName);
            _applicationInsightsHelper.TrackMetric("Blob record count", factsDataTable.Rows.Count, logFileName);

            return factsDataTable;
        }

        public async Task<DataTable> CreateAsync(IReadOnlyCollection<ToolStatistics> sourceData, string logFileName)
        {
            var stopwatch = Stopwatch.StartNew();

            // insert any new dimension data first
            if (_times == null)
            {
                // this call is only needed once in the lifetime of the service
                _times = await GetDimension("time", logFileName, connection => RetrieveTimeDimensions(connection));
            }

            var clientsTask = GetDimension("client", logFileName, connection => RetrieveClientDimensions(sourceData, connection));
            var platformsTask = GetDimension("platform", logFileName, connection => RetrievePlatformDimensions(sourceData, connection));
            var datesTask = GetDimension("date", logFileName, connection => RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered)));
            var toolsTask = GetDimension("tool", logFileName, connection => RetrieveToolDimensions(sourceData, connection));
            var userAgentsTask = GetDimension("useragent", logFileName, connection => RetrieveUserAgentFacts(sourceData, connection));
            var logFileNamesTask = GetDimension("logfilename", logFileName, connection => RetrieveLogFileNameFacts(logFileName, connection));

            await Task.WhenAll(clientsTask, platformsTask, datesTask, toolsTask, userAgentsTask, logFileNamesTask);

            var clients = clientsTask.Result;
            var platforms = platformsTask.Result;
            var dates = datesTask.Result;
            var tools = toolsTask.Result;
            var userAgents = userAgentsTask.Result;
            var logFileNames = logFileNamesTask.Result;

            // create facts data rows by linking source data with dimensions
            var dataImporter = new DataImporter(_openStatisticsSqlConnectionAsync);
            var dataTable = await dataImporter.GetDataTableAsync(DataImporter.Table.Fact_Dist_Download);

            var knownClientsAvailable = clients.Any();
            var knownPlatformsAvailable = platforms.Any();
            var knownUserAgentsAvailable = userAgents.Any();

            int logFileNameId = DimensionId.Unknown;
            if (logFileNames.Any() && logFileNames.ContainsKey(logFileName))
            {
                logFileNameId = logFileNames[logFileName];
            }

            _logger.LogDebug("Creating tools facts...");

            foreach (var groupedByToolId in sourceData.GroupBy(e => e.ToolId, StringComparer.OrdinalIgnoreCase))
            {
                var toolsForId = tools.Where(e => string.Equals(e.ToolId, groupedByToolId.Key, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var groupedByToolIdAndVersion in groupedByToolId.GroupBy(e => e.ToolVersion, StringComparer.OrdinalIgnoreCase))
                {
                    var toolVersion = groupedByToolIdAndVersion.Key;
                    var toolsForIdAndVersion = toolsForId.Where(e => string.Equals(e.ToolVersion, toolVersion, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var groupdByToolIdAndVersionAndFileName in groupedByToolIdAndVersion.GroupBy(e => e.FileName, StringComparer.OrdinalIgnoreCase))
                    {
                        var fileName = groupdByToolIdAndVersionAndFileName.Key;
                        var tool = toolsForIdAndVersion.FirstOrDefault(e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));

                        int toolId;
                        if (tool == null)
                        {
                            // Track it in Application Insights.
                            _applicationInsightsHelper.TrackToolNotFound(groupedByToolId.Key, toolVersion, fileName, logFileName);

                            continue;
                        }
                        else
                        {
                            toolId = tool.Id;
                        }

                        foreach (var element in groupedByToolIdAndVersion)
                        {
                            // required dimensions
                            var dateId = dates.First(e => e.Date.Equals(element.EdgeServerTimeDelivered.Date)).Id;
                            var timeId = _times.First(e => e.HourOfDay == element.EdgeServerTimeDelivered.Hour).Id;

                            // dimensions that could be "(unknown)"
                            int platformId = DimensionId.Unknown;
                            if (knownPlatformsAvailable && platforms.ContainsKey(element.UserAgent))
                            {
                                platformId = platforms[element.UserAgent];
                            }

                            int clientId = DimensionId.Unknown;
                            if (knownClientsAvailable && clients.ContainsKey(element.UserAgent))
                            {
                                clientId = clients[element.UserAgent];
                            }

                            int userAgentId = DimensionId.Unknown;
                            if (knownUserAgentsAvailable)
                            {
                                var trimmedUserAgent = UserAgentFact.TrimUserAgent(element.UserAgent);
                                if (userAgents.ContainsKey(trimmedUserAgent))
                                {
                                    userAgentId = userAgents[trimmedUserAgent];
                                }
                            }

                            var dataRow = dataTable.NewRow();
                            FillToolDataRow(dataRow, dateId, timeId, toolId, platformId, clientId, userAgentId, logFileNameId);
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }

            stopwatch.Stop();
            _logger.LogDebug("  DONE (" + dataTable.Rows.Count + " records, " + stopwatch.ElapsedMilliseconds + "ms)");

            return dataTable;
        }

        public async Task StoreLogFileAggregatesAsync(LogFileAggregates logFileAggregates)
        {
            _logger.LogDebug("Storing log file aggregates...");

            try
            {
                await SqlRetryUtility.RetrySql(() => RunStoreLogFileAggregatesQueryAsync(logFileAggregates));
            }
            catch (Exception exception)
            {
                _logger.LogError("Failed to insert log file aggregates for {LogFile}.", logFileAggregates.LogFileName);

                _applicationInsightsHelper.TrackException(exception, logFileAggregates.LogFileName);

                throw;
            }

            _logger.LogDebug("  DONE");
        }

        private async Task RunStoreLogFileAggregatesQueryAsync(LogFileAggregates logFileAggregates)
        {
            using (var connection = await _openStatisticsSqlConnectionAsync())
            {
                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[StoreLogFileAggregates]";
                command.CommandTimeout = _defaultCommandTimeout;
                command.CommandType = CommandType.StoredProcedure;

                var parameterValue = CreateDataTableForLogFileAggregatesPackageDownloadsByDate(logFileAggregates);
                var parameter = command.Parameters.AddWithValue("packageDownloadsByDate", parameterValue);
                parameter.SqlDbType = SqlDbType.Structured;
                parameter.TypeName = "[dbo].[LogFileAggregatesPackageDownloadsByDateTableType]";

                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<IReadOnlyCollection<string>> GetAlreadyAggregatedLogFilesAsync()
        {
            _logger.LogDebug("Retrieving already processed log files...");

            var alreadyAggregatedLogFiles = new List<string>();
            using (var connection = await _openStatisticsSqlConnectionAsync())
            {
                try
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "[dbo].[SelectAlreadyAggregatedLogFiles]";
                    command.CommandTimeout = _defaultCommandTimeout;
                    command.CommandType = CommandType.StoredProcedure;

                    using (var dataReader = await command.ExecuteReaderAsync())
                    {
                        while (await dataReader.ReadAsync())
                        {
                            alreadyAggregatedLogFiles.Add(dataReader.GetString(0));
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError("Failed to retrieve already aggregated log files", exception);

                    _applicationInsightsHelper.TrackException(exception);

                    throw;
                }
            }

            _logger.LogDebug("  DONE");

            return alreadyAggregatedLogFiles;
        }

        public async Task<bool> HasImportedToolStatisticsAsync(string logFileName)
        {
            _logger.LogDebug("Checking if we already processed tool statistics in {LogFileName}...", logFileName);

            return await HasImportedStatisticsAsync(
                logFileName,
                "[dbo].[CheckLogFileHasToolStatistics]",
                LogEvents.FailedToCheckAlreadyProcessedLogFileToolStatistics);
        }

        public async Task<bool> HasImportedPackageStatisticsAsync(string logFileName)
        {
            _logger.LogDebug("Checking if we already processed package statistics in {LogFileName}...", logFileName);

            return await HasImportedStatisticsAsync(
                logFileName,
                "[dbo].[CheckLogFileHasPackageStatistics]",
                LogEvents.FailedToCheckAlreadyProcessedLogFilePackageStatistics);
        }

        private async Task<bool> HasImportedStatisticsAsync(string logFileName, string commandText, EventId errorEventId)
        {
            int hasStatistics;

            try
            {
                using (var connection = await _openStatisticsSqlConnectionAsync())
                {
                    var command = connection.CreateCommand();
                    command.CommandText = commandText;
                    command.CommandTimeout = _defaultCommandTimeout;
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("logFileName", logFileName);

                    hasStatistics = (int)await command.ExecuteScalarAsync();
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    errorEventId,
                    exception,
                    errorEventId.Name + " {LogFileName}...",
                    logFileName);

                _applicationInsightsHelper.TrackException(exception);

                throw;
            }

            return hasStatistics == 1;
        }

        private async Task<IDictionary<string, int>> GetDimension(string dimension, string logFileName, Func<SqlConnection, Task<IDictionary<string, int>>> retrieve)
        {
            var stopwatch = Stopwatch.StartNew();
            var count = _maxRetryCount;

            using (_logger.BeginScope("Getting dimension {Dimension} for log file {LogFile}", dimension, logFileName))
            {
                while (count > 0)
                {
                    try
                    {
                        _logger.LogDebug("Beginning to retrieve dimension '{Dimension}'.", dimension);

                        IDictionary<string, int> dimensions;
                        using (var connection = await _openStatisticsSqlConnectionAsync())
                        {
                            dimensions = await retrieve(connection);
                        }

                        stopwatch.Stop();

                        _logger.LogInformation("Finished retrieving dimension '{Dimension}' ({RetrievedDimensionDuration} ms).", dimension, stopwatch.ElapsedMilliseconds);
                        _applicationInsightsHelper.TrackRetrieveDimensionDuration(dimension, stopwatch.ElapsedMilliseconds, logFileName);

                        return dimensions;
                    }
                    catch (SqlException e)
                    {
                        --count;
                        if (count <= 0)
                        {
                            throw;
                        }

                        if (e.Number == 1205)
                        {
                            _logger.LogWarning("SQL Deadlock, retrying...");
                            _applicationInsightsHelper.TrackSqlException("SQL Deadlock", e, logFileName, dimension);
                        }
                        else if (e.Number == -2)
                        {
                            _logger.LogWarning("SQL Timeout, retrying...");
                            _applicationInsightsHelper.TrackSqlException("SQL Timeout", e, logFileName, dimension);
                        }
                        else if (e.Number == 2601)
                        {
                            _logger.LogWarning("SQL Duplicate key, retrying...");
                            _applicationInsightsHelper.TrackSqlException("SQL Duplicate Key", e, logFileName, dimension);
                        }
                        else
                        {
                            throw;
                        }

                        Task.Delay(_retryDelay).Wait();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(LogEvents.FailedDimensionRetrieval, exception, "Failed to retrieve dimension '{Dimension}'.", dimension);
                        _applicationInsightsHelper.TrackException(exception, logFileName);

                        if (stopwatch.IsRunning)
                            stopwatch.Stop();

                        throw;
                    }
                }
            }

            return new Dictionary<string, int>();
        }

        private async Task<IReadOnlyCollection<T>> GetDimension<T>(string dimension, string logFileName, Func<SqlConnection, Task<IReadOnlyCollection<T>>> retrieve)
        {
            var stopwatch = Stopwatch.StartNew();
            var count = _maxRetryCount;

            using (_logger.BeginScope("Getting dimension {Dimension} for log file {LogFile}", dimension, logFileName))
            {
                while (count > 0)
                {
                    try
                    {
                        _logger.LogDebug("Beginning to retrieve dimension '{Dimension}'.", dimension);

                        IReadOnlyCollection<T> dimensions;
                        using (var connection = await _openStatisticsSqlConnectionAsync())
                        {
                            dimensions = await retrieve(connection);
                        }

                        stopwatch.Stop();

                        _logger.LogInformation("Finished retrieving dimension '{Dimension}' ({RetrievedDimensionDuration} ms).", dimension, stopwatch.ElapsedMilliseconds);
                        _applicationInsightsHelper.TrackRetrieveDimensionDuration(dimension, stopwatch.ElapsedMilliseconds, logFileName);

                        return dimensions;
                    }
                    catch (SqlException e)
                    {
                        --count;
                        if (count <= 0)
                        {
                            throw;
                        }

                        if (e.Number == 1205)
                        {
                            _logger.LogWarning("SQL Deadlock, retrying...");
                            _applicationInsightsHelper.TrackSqlException("SQL Deadlock", e, logFileName, dimension);
                        }
                        else if (e.Number == -2)
                        {
                            _logger.LogWarning("SQL Timeout, retrying...");
                            _applicationInsightsHelper.TrackSqlException("SQL Timeout", e, logFileName, dimension);
                        }
                        else if (e.Number == 2601)
                        {
                            _logger.LogWarning("SQL Duplicate key, retrying...");
                            _applicationInsightsHelper.TrackSqlException("SQL Duplicate Key", e, logFileName, dimension);
                        }
                        else
                        {
                            throw;
                        }

                        Task.Delay(_retryDelay).Wait();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(LogEvents.FailedDimensionRetrieval, exception, "Failed to retrieve dimension '{Dimension}'.", dimension);
                        _applicationInsightsHelper.TrackException(exception, logFileName);

                        if (stopwatch.IsRunning)
                            stopwatch.Stop();

                        throw;
                    }
                }
            }

            return Enumerable.Empty<T>().ToList();
        }

        private static void FillDataRow(DataRow dataRow, int dateId, int timeId, int packageId, int operationId, int platformId, int clientId, int userAgentId, int logFileNameId, int downloadCount)
        {
            dataRow["Dimension_Package_Id"] = packageId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Operation_Id"] = operationId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
            dataRow["Fact_UserAgent_Id"] = userAgentId;
            dataRow["Fact_LogFileName_Id"] = logFileNameId;
            dataRow["DownloadCount"] = downloadCount;
        }

        private static void FillToolDataRow(DataRow dataRow, int dateId, int timeId, int toolId, int platformId, int clientId, int userAgentId, int logFileNameId)
        {
            dataRow["Dimension_Tool_Id"] = toolId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
            dataRow["Fact_UserAgent_Id"] = userAgentId;
            dataRow["Fact_LogFileName_Id"] = logFileNameId;
            dataRow["DownloadCount"] = 1;
        }

        private async Task<IReadOnlyCollection<ToolDimension>> RetrieveToolDimensions(IReadOnlyCollection<ToolStatistics> sourceData, SqlConnection connection)
        {
            var tools = sourceData
                   .Select(e => new ToolDimension(e.ToolId, e.ToolVersion, e.FileName))
                   .Distinct()
                   .ToList();

            var results = new List<ToolDimension>();
            if (!tools.Any())
            {
                return results;
            }

            results.AddRange(_cachedToolDimensions.Where(p1 => tools.FirstOrDefault(p2 => p2.Equals(p1)) != null));

            var nonCachedToolDimensions = tools.Except(results).ToList();

            if (nonCachedToolDimensions.Any())
            {
                var parameterValue = CreateDataTable(nonCachedToolDimensions);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsureToolDimensionsExist]";
                command.CommandTimeout = _defaultCommandTimeout;
                command.CommandType = CommandType.StoredProcedure;

                var parameter = command.Parameters.AddWithValue("tools", parameterValue);
                parameter.SqlDbType = SqlDbType.Structured;
                parameter.TypeName = "[dbo].[ToolDimensionTableType]";

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var tool = new ToolDimension(dataReader.GetString(1), dataReader.GetString(2), dataReader.GetString(3))
                        {
                            Id = dataReader.GetInt32(0)
                        };

                        if (!results.Contains(tool))
                        {
                            results.Add(tool);
                        }
                        if (!_cachedToolDimensions.Contains(tool))
                        {
                            _cachedToolDimensions.Add(tool);
                        }
                    }
                }
            }

            return results;
        }

        private async Task<IReadOnlyCollection<PackageDimension>> RetrievePackageDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var results = new List<PackageDimension>();
            var nonCachedPackageDimensions = new List<PackageDimension>();
            var sourceDataPackages = new HashSet<PackageDimension>();

            foreach (var sourceStatistics in sourceData)
            {
                var sourcePackage = new PackageDimension(sourceStatistics.PackageId, sourceStatistics.PackageVersion);
                if (!sourceDataPackages.Add(sourcePackage))
                {
                    // This package has already been seen in the sourceData
                    continue;
                }

                if (_cachedPackageDimensions.TryGetValue(sourcePackage, out var cachedPackage))
                {
                    results.Add(cachedPackage);
                }
                else
                {
                    nonCachedPackageDimensions.Add(sourcePackage);
                }
            }

            if (nonCachedPackageDimensions.Any())
            {
                var parameterValue = CreateDataTable(nonCachedPackageDimensions);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsurePackageDimensionsExist]";
                command.CommandTimeout = _defaultCommandTimeout;
                command.CommandType = CommandType.StoredProcedure;

                var parameter = command.Parameters.AddWithValue("packages", parameterValue);
                parameter.SqlDbType = SqlDbType.Structured;
                parameter.TypeName = "[dbo].[PackageDimensionTableType]";

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var package = new PackageDimension(dataReader.GetString(1), dataReader.GetString(2))
                        {
                            Id = dataReader.GetInt32(0)
                        };

                        results.Add(package);
                        _cachedPackageDimensions[package] = package;
                    }
                }
            }

            return results;
        }

        private static async Task<IReadOnlyCollection<DateDimension>> RetrieveDateDimensions(SqlConnection connection, DateTime min, DateTime max)
        {
            var results = new List<DateDimension>();

            var command = SqlQueries.GetDateDimensions(connection, _defaultCommandTimeout, min, max);

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var result = new DateDimension
                    {
                        Id = dataReader.GetInt32(0),
                        Date = dataReader.GetDateTime(1)
                    };

                    results.Add(result);
                }
            }

            return results;
        }

        private static async Task<IReadOnlyCollection<TimeDimension>> RetrieveTimeDimensions(SqlConnection connection)
        {
            var results = new List<TimeDimension>();

            var command = SqlQueries.GetAllTimeDimensions(connection, _defaultCommandTimeout);

            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var result = new TimeDimension
                    {
                        Id = dataReader.GetInt32(0),
                        HourOfDay = dataReader.GetInt32(1)
                    };

                    results.Add(result);
                }
            }

            return results;
        }

        private async Task<IDictionary<string, int>> RetrieveOperationDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
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

            var nonCachedOperations = new List<string>();
            foreach (var operation in operations)
            {
                if (_cachedOperationDimensions.ContainsKey(operation))
                {
                    var cachedOperationDimensionId = _cachedOperationDimensions[operation];
                    results.Add(operation, cachedOperationDimensionId);
                }
                else
                {
                    nonCachedOperations.Add(operation);
                }
            }

            if (nonCachedOperations.Any())
            {
                var operationsParameter = string.Join(",", nonCachedOperations);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsureOperationDimensionsExist]";
                command.CommandTimeout = _defaultCommandTimeout;
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("operations", operationsParameter);

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var operation = dataReader.GetString(1);
                        var operationId = dataReader.GetInt32(0);

                        if (!_cachedOperationDimensions.ContainsKey(operation))
                        {
                            _cachedOperationDimensions.Add(operation, operationId);
                        }

                        results.Add(operation, operationId);
                    }
                }
            }

            return results;
        }

        private async Task<IDictionary<string, int>> RetrieveClientDimensions(IReadOnlyCollection<ITrackUserAgent> sourceData, SqlConnection connection)
        {
            var clientDimensions = sourceData
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent)
                .Select(e => e.First())
                .ToDictionary(e => e.UserAgent, statistics => ClientDimension.FromUserAgent(statistics.UserAgent));

            var results = new Dictionary<string, int>();
            if (!clientDimensions.Any())
            {
                return results;
            }

            var nonCachedClientDimensions = new Dictionary<string, ClientDimension>();
            foreach (var clientDimension in clientDimensions)
            {
                if (_cachedClientDimensions.ContainsKey(clientDimension.Key))
                {
                    var cachedClientDimensionId = _cachedClientDimensions[clientDimension.Key];
                    results.Add(clientDimension.Key, cachedClientDimensionId);
                }
                else
                {
                    nonCachedClientDimensions.Add(clientDimension.Key, clientDimension.Value);
                }
            }

            if (nonCachedClientDimensions.Any())
            {
                var parameterValue = ClientDimensionTableType.CreateDataTable(nonCachedClientDimensions);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsureClientDimensionsExist]";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _defaultCommandTimeout;

                var parameter = command.Parameters.AddWithValue("clients", parameterValue);
                parameter.SqlDbType = SqlDbType.Structured;
                parameter.TypeName = "[dbo].[ClientDimensionTableType]";

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var userAgent = dataReader.GetString(1);
                        var clientDimensionId = dataReader.GetInt32(0);

                        if (!_cachedClientDimensions.ContainsKey(userAgent))
                        {
                            _cachedClientDimensions.Add(userAgent, clientDimensionId);
                        }

                        results.Add(userAgent, clientDimensionId);
                    }
                }
            }

            return results;
        }

        private async Task<IDictionary<string, int>> RetrievePlatformDimensions(IReadOnlyCollection<ITrackUserAgent> sourceData, SqlConnection connection)
        {
            var platformDimensions = sourceData
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent)
                .Select(e => e.First())
                .ToDictionary(e => e.UserAgent, PlatformDimension.FromUserAgent);

            var results = new Dictionary<string, int>();
            if (!platformDimensions.Any())
            {
                return results;
            }

            var nonCachedPlatformDimensions = new Dictionary<string, PlatformDimension>();
            foreach (var platformDimension in platformDimensions)
            {
                if (_cachedPlatformDimensions.ContainsKey(platformDimension.Key))
                {
                    var cachedPlatformDimensionId = _cachedPlatformDimensions[platformDimension.Key];
                    results.Add(platformDimension.Key, cachedPlatformDimensionId);
                }
                else
                {
                    nonCachedPlatformDimensions.Add(platformDimension.Key, platformDimension.Value);
                }
            }

            if (nonCachedPlatformDimensions.Any())
            {
                var parameterValue = CreateDataTable(nonCachedPlatformDimensions);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsurePlatformDimensionsExist]";
                command.CommandTimeout = _defaultCommandTimeout;
                command.CommandType = CommandType.StoredProcedure;

                var parameter = command.Parameters.AddWithValue("platforms", parameterValue);
                parameter.SqlDbType = SqlDbType.Structured;
                parameter.TypeName = "[dbo].[PlatformDimensionTableType]";

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var platform = dataReader.GetString(1);
                        var platformId = dataReader.GetInt32(0);

                        if (!_cachedPlatformDimensions.ContainsKey(platform))
                        {
                            _cachedPlatformDimensions.Add(platform, platformId);
                        }

                        results.Add(platform, platformId);
                    }
                }
            }

            return results;
        }

        private async Task<IDictionary<string, int>> RetrieveUserAgentFacts(IReadOnlyCollection<ITrackUserAgent> sourceData, SqlConnection connection)
        {
            var userAgents = sourceData
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent)
                .Select(e => e.First())
                .Select(e => UserAgentFact.TrimUserAgent(e.UserAgent))
                .ToList();

            var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!userAgents.Any())
            {
                return results;
            }

            var nonCachedUserAgents = new List<string>();
            foreach (var userAgent in userAgents)
            {
                if (_cachedUserAgentFacts.ContainsKey(userAgent))
                {
                    var cachedUserAgentFactId = _cachedUserAgentFacts[userAgent];
                    results.Add(userAgent, cachedUserAgentFactId);
                }
                else if (!nonCachedUserAgents.Contains(userAgent))
                {
                    nonCachedUserAgents.Add(userAgent);
                }
            }

            if (nonCachedUserAgents.Any())
            {
                var parameterValue = UserAgentFactTableType.CreateDataTable(nonCachedUserAgents.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

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
                        var userAgent = dataReader.GetString(1);
                        var userAgentId = dataReader.GetInt32(0);

                        if (!_cachedUserAgentFacts.ContainsKey(userAgent))
                        {
                            _cachedUserAgentFacts.Add(userAgent, userAgentId);
                        }

                        if (!results.ContainsKey(userAgent))
                        {
                            results.Add(userAgent, userAgentId);
                        }
                    }
                }
            }

            return results;
        }

        private async Task<IDictionary<string, int>> RetrieveLogFileNameFacts(string logFileName, SqlConnection connection)
        {
            var results = new Dictionary<string, int>();

            var parameterValue = CreateDataTable(logFileName);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureLogFileNameFactsExist]";
            command.CommandTimeout = _defaultCommandTimeout;
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.Parameters.AddWithValue("logfilenames", parameterValue);
            parameter.SqlDbType = SqlDbType.Structured;
            parameter.TypeName = "[dbo].[LogFileNameFactTableType]";

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
            table.Columns.Add("Major", typeof(string));
            table.Columns.Add("Minor", typeof(string));
            table.Columns.Add("Patch", typeof(string));
            table.Columns.Add("PatchMinor", typeof(string));

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

        private static DataTable CreateDataTable(IDictionary<string, IpAddressFact> ipAddressFacts)
        {
            var table = new DataTable();
            table.Columns.Add("Address", typeof(byte[]));
            table.Columns.Add("TextAddress", typeof(string));

            foreach (var ipAddress in ipAddressFacts)
            {
                var row = table.NewRow();
                row["Address"] = ipAddress.Value.IpAddressBytes;
                row["TextAddress"] = ipAddress.Key;

                table.Rows.Add(row);
            }
            return table;
        }

        private static DataTable CreateDataTable(IReadOnlyCollection<ToolDimension> toolDimensions)
        {
            var table = new DataTable();
            table.Columns.Add("ToolId", typeof(string));
            table.Columns.Add("ToolVersion", typeof(string));
            table.Columns.Add("FileName", typeof(string));

            foreach (var toolDimension in toolDimensions)
            {
                var row = table.NewRow();
                row["ToolId"] = toolDimension.ToolId;
                row["ToolVersion"] = toolDimension.ToolVersion;
                row["FileName"] = toolDimension.FileName;

                table.Rows.Add(row);
            }

            return table;
        }

        private static DataTable CreateDataTable(string logFileName)
        {
            var table = new DataTable();
            table.Columns.Add("LogFileName", typeof(string));

            var row = table.NewRow();
            row["LogFileName"] = logFileName;

            table.Rows.Add(row);

            return table;
        }

        private static DataTable CreateDataTableForLogFileAggregatesPackageDownloadsByDate(LogFileAggregates logFileAggregates)
        {
            var table = new DataTable();
            table.Columns.Add("LogFileName", typeof(string));
            table.Columns.Add("Date_Dimension_Id", typeof(int));
            table.Columns.Add("PackageDownloads", typeof(int));

            foreach (var kvp in logFileAggregates.PackageDownloadsByDateDimensionId)
            {
                var row = table.NewRow();
                row["LogFileName"] = logFileAggregates.LogFileName;
                row["Date_Dimension_Id"] = kvp.Key;
                row["PackageDownloads"] = kvp.Value;

                table.Rows.Add(row);
            }

            return table;
        }
    }
}