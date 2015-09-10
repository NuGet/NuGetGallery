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
        private const int _defaultCommandTimeout = 1800; // 30 minutes max
        private const int _maxRetryCount = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);
        private readonly JobEventSource _jobEventSource;
        private readonly SqlConnectionStringBuilder _targetDatabase;
        private static IReadOnlyCollection<TimeDimension> _times;
        private static readonly IList<PackageDimension> _cachedPackageDimensions = new List<PackageDimension>();
        private static readonly IList<ToolDimension> _cachedToolDimensions = new List<ToolDimension>();
        private static readonly IDictionary<string, int> _cachedClientDimensions = new Dictionary<string, int>();
        private static readonly IDictionary<string, int> _cachedPlatformDimensions = new Dictionary<string, int>();
        private static readonly IDictionary<string, int> _cachedOperationDimensions = new Dictionary<string, int>();
        private static readonly IDictionary<string, int> _cachedProjectTypeDimensions = new Dictionary<string, int>();
        private static readonly IDictionary<string, int> _cachedUserAgentFacts = new Dictionary<string, int>();
        private static readonly IDictionary<string, int> _cachedIpAddressFacts = new Dictionary<string, int>();

        public Warehouse(JobEventSource jobEventSource, SqlConnectionStringBuilder targetDatabase)
        {
            _jobEventSource = jobEventSource;
            _targetDatabase = targetDatabase;
        }

        internal async Task InsertDownloadFactsAsync(DataTable downloadFacts, string logFileName)
        {
            Trace.WriteLine("Inserting into facts table...");
            var stopwatch = Stopwatch.StartNew();

            using (var connection = await _targetDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
                bulkCopy.BatchSize = 25000;
                bulkCopy.DestinationTableName = downloadFacts.TableName;
                bulkCopy.BulkCopyTimeout = _defaultCommandTimeout;

                try
                {
                    await bulkCopy.WriteToServerAsync(downloadFacts);

                    transaction.Commit();

                    stopwatch.Stop();
                    ApplicationInsights.TrackMetric("Insert facts duration (ms)", stopwatch.ElapsedMilliseconds, logFileName);
                }
                catch (Exception exception)
                {
                    if (stopwatch.IsRunning)
                    {
                        stopwatch.Stop();
                    }

                    ApplicationInsights.TrackException(exception, logFileName);
                    transaction.Rollback();
                    throw;
                }
            }

            Trace.Write("  DONE");
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
            var projectTypesTask = GetDimension("project type", logFileName, connection => RetrieveProjectTypeDimensions(sourceData, connection));
            var clientsTask = GetDimension("client", logFileName, connection => RetrieveClientDimensions(sourceData, connection));
            var platformsTask = GetDimension("platform", logFileName, connection => RetrievePlatformDimensions(sourceData, connection));
            var datesTask = GetDimension("date", logFileName, connection => RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered)));
            var packageTranslationsTask = GetDimension("package translations", logFileName, connection => RetrievePackageTranslations(sourceData, connection));
            var userAgentsTask = GetDimension("useragent", logFileName, connection => RetrieveUserAgentFacts(sourceData, connection));
            var logFileNamesTask = GetDimension("logfilename", logFileName, connection => RetrieveLogFileNameFacts(logFileName, connection));
            var ipAddressesTask = GetDimension("ipaddress", logFileName, connection => RetrieveIpAddressesFacts(sourceData, connection));

            await Task.WhenAll(operationsTask, projectTypesTask, clientsTask, platformsTask, datesTask, packagesTask, packageTranslationsTask, userAgentsTask, logFileNamesTask, ipAddressesTask);

            var operations = operationsTask.Result;
            var projectTypes = projectTypesTask.Result;
            var clients = clientsTask.Result;
            var platforms = platformsTask.Result;
            var userAgents = userAgentsTask.Result;
            var logFileNames = logFileNamesTask.Result;
            var ipAddresses = ipAddressesTask.Result;

            var dates = datesTask.Result;
            var packages = packagesTask.Result;
            var packageTranslations = packageTranslationsTask.Result;

            // create facts data rows by linking source data with dimensions
            var dataImporter = new DataImporter(_targetDatabase);
            var dataTable = await dataImporter.GetDataTableAsync("Fact_Download");

            var knownOperationsAvailable = operations.Any();
            var knownProjectTypesAvailable = projectTypes.Any();
            var knownClientsAvailable = clients.Any();
            var knownPlatformsAvailable = platforms.Any();
            var knownUserAgentsAvailable = userAgents.Any();
            var knownIpAddressesAvailable = ipAddresses.Any();

            int logFileNameId = DimensionId.Unknown;
            if (logFileNames.Any() && logFileNames.ContainsKey(logFileName))
            {
                logFileNameId = logFileNames[logFileName];
            }

            Trace.WriteLine("Creating facts...");
            foreach (var groupedByPackageId in sourceData.GroupBy(e => e.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var packagesForId = packages.Where(e => string.Equals(e.PackageId, groupedByPackageId.Key, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var groupedByPackageIdAndVersion in groupedByPackageId.GroupBy(e => e.PackageVersion, StringComparer.OrdinalIgnoreCase))
                {
                    int packageId;
                    var package = packagesForId.FirstOrDefault(e => string.Equals(e.PackageVersion, groupedByPackageIdAndVersion.Key, StringComparison.OrdinalIgnoreCase));
                    if (package == null)
                    {
                        // This package id and version could not be 100% accurately parsed from the CDN Request URL,
                        // likely due to weird package ID which could be interpreted as a version string.
                        // Look for a mapping in the support table in an attempt to auto-correct this entry.
                        var packageTranslation = packageTranslations.FirstOrDefault(t => t.IncorrectPackageId == groupedByPackageId.Key && t.IncorrectPackageVersion == groupedByPackageIdAndVersion.Key);
                        if (packageTranslation != null)
                        {
                            // there seems to be a mapping
                            packageId = packageTranslation.CorrectedPackageId;
                        }
                        else
                        {
                            // Track it in Application Insights.
                            ApplicationInsights.TrackPackageNotFound(groupedByPackageId.Key, groupedByPackageIdAndVersion.Key, logFileName);

                            continue;
                        }
                    }
                    else
                    {
                        packageId = package.Id;
                    }

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
                        if (knownUserAgentsAvailable && userAgents.ContainsKey(element.UserAgent))
                        {
                            userAgentId = userAgents[element.UserAgent];
                        }

                        int edgeServerIpAddressId = DimensionId.Unknown;
                        if (!string.IsNullOrEmpty(element.EdgeServerIpAddress) && knownIpAddressesAvailable && ipAddresses.ContainsKey(element.EdgeServerIpAddress))
                        {
                            edgeServerIpAddressId = ipAddresses[element.EdgeServerIpAddress];
                        }

                        int projectTypeId = DimensionId.Unknown;
                        if (knownProjectTypesAvailable)
                        {
                            // foreach project type
                            foreach (var projectGuid in element.ProjectGuids.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                projectTypeId = projectTypes[projectGuid];

                                var dataRow = dataTable.NewRow();
                                FillDataRow(dataRow, dateId, timeId, packageId, operationId, platformId, projectTypeId, clientId, userAgentId, logFileNameId, edgeServerIpAddressId);
                                dataTable.Rows.Add(dataRow);
                            }
                        }
                        else
                        {
                            var dataRow = dataTable.NewRow();
                            FillDataRow(dataRow, dateId, timeId, packageId, operationId, platformId, projectTypeId, clientId, userAgentId, logFileNameId, edgeServerIpAddressId);
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }
            stopwatch.Stop();
            Trace.Write("  DONE (" + dataTable.Rows.Count + " records, " + stopwatch.ElapsedMilliseconds + "ms)");
            ApplicationInsights.TrackMetric("Blob record count", dataTable.Rows.Count, logFileName);

            return dataTable;
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
            var ipAddressesTask = GetDimension("ipaddress", logFileName, connection => RetrieveIpAddressesFacts(sourceData, connection));

            await Task.WhenAll(clientsTask, platformsTask, datesTask, toolsTask, userAgentsTask, logFileNamesTask, ipAddressesTask);

            var clients = clientsTask.Result;
            var platforms = platformsTask.Result;
            var dates = datesTask.Result;
            var tools = toolsTask.Result;
            var userAgents = userAgentsTask.Result;
            var logFileNames = logFileNamesTask.Result;
            var ipAddresses = ipAddressesTask.Result;

            // create facts data rows by linking source data with dimensions
            var dataImporter = new DataImporter(_targetDatabase);
            var dataTable = await dataImporter.GetDataTableAsync("Fact_Dist_Download");

            var knownClientsAvailable = clients.Any();
            var knownPlatformsAvailable = platforms.Any();
            var knownUserAgentsAvailable = userAgents.Any();
            var knownIpAddressesAvailable = ipAddresses.Any();

            int logFileNameId = DimensionId.Unknown;
            if (logFileNames.Any() && logFileNames.ContainsKey(logFileName))
            {
                logFileNameId = logFileNames[logFileName];
            }

            Trace.WriteLine("Creating tools facts...");

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
                            ApplicationInsights.TrackToolNotFound(groupedByToolId.Key, toolVersion, fileName, logFileName);

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
                            if (knownUserAgentsAvailable && userAgents.ContainsKey(element.UserAgent))
                            {
                                userAgentId = userAgents[element.UserAgent];
                            }

                            int edgeServerIpAddressId = DimensionId.Unknown;
                            if (!string.IsNullOrEmpty(element.EdgeServerIpAddress) && knownIpAddressesAvailable && ipAddresses.ContainsKey(element.EdgeServerIpAddress))
                            {
                                edgeServerIpAddressId = ipAddresses[element.EdgeServerIpAddress];
                            }

                            var dataRow = dataTable.NewRow();
                            FillToolDataRow(dataRow, dateId, timeId, toolId, platformId, clientId, userAgentId, logFileNameId, edgeServerIpAddressId);
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }

            stopwatch.Stop();
            Trace.Write("  DONE (" + dataTable.Rows.Count + " records, " + stopwatch.ElapsedMilliseconds + "ms)");

            return dataTable;
        }

        private async Task<IDictionary<string, int>> GetDimension(string dimension, string logFileName, Func<SqlConnection, Task<IDictionary<string, int>>> retrieve)
        {
            var stopwatch = Stopwatch.StartNew();
            var count = _maxRetryCount;

            while (count > 0)
            {
                try
                {
                    _jobEventSource.BeginningRetrieveDimension(dimension);

                    IDictionary<string, int> dimensions;
                    using (var connection = await _targetDatabase.ConnectTo())
                    {
                        dimensions = await retrieve(connection);
                    }

                    stopwatch.Stop();
                    _jobEventSource.FinishedRetrieveDimension(dimension, stopwatch.ElapsedMilliseconds);
                    ApplicationInsights.TrackRetrieveDimensionDuration(dimension, stopwatch.ElapsedMilliseconds, logFileName);

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
                        Trace.TraceWarning("Deadlock, retrying...");
                        ApplicationInsights.TrackSqlException("SQL Deadlock", e, logFileName, dimension);
                    }
                    else if (e.Number == -2)
                    {
                        Trace.TraceWarning("Timeout, retrying...");
                        ApplicationInsights.TrackSqlException("SQL Timeout", e, logFileName, dimension);
                    }
                    else if (e.Number == 2601)
                    {
                        Trace.TraceWarning("Duplicate key, retrying...");
                        ApplicationInsights.TrackSqlException("SQL Duplicate Key", e, logFileName, dimension);
                    }
                    else
                    {
                        throw;
                    }

                    Task.Delay(_retryDelay).Wait();
                }
                catch (Exception exception)
                {
                    _jobEventSource.FailedRetrieveDimension(dimension);
                    ApplicationInsights.TrackException(exception, logFileName);

                    if (stopwatch.IsRunning)
                        stopwatch.Stop();

                    throw;
                }
            }

            return new Dictionary<string, int>();
        }

        private async Task<IReadOnlyCollection<T>> GetDimension<T>(string dimension, string logFileName, Func<SqlConnection, Task<IReadOnlyCollection<T>>> retrieve)
        {
            var stopwatch = Stopwatch.StartNew();
            var count = _maxRetryCount;

            while (count > 0)
            {
                try
                {
                    _jobEventSource.BeginningRetrieveDimension(dimension);

                    IReadOnlyCollection<T> dimensions;
                    using (var connection = await _targetDatabase.ConnectTo())
                    {
                        dimensions = await retrieve(connection);
                    }

                    stopwatch.Stop();
                    _jobEventSource.FinishedRetrieveDimension(dimension, stopwatch.ElapsedMilliseconds);
                    ApplicationInsights.TrackRetrieveDimensionDuration(dimension, stopwatch.ElapsedMilliseconds, logFileName);

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
                        Trace.TraceWarning("Deadlock, retrying...");
                        ApplicationInsights.TrackSqlException("SQL Deadlock", e, logFileName, dimension);
                    }
                    else if (e.Number == -2)
                    {
                        Trace.TraceWarning("Timeout, retrying...");
                        ApplicationInsights.TrackSqlException("SQL Timeout", e, logFileName, dimension);
                    }
                    else if (e.Number == 2601)
                    {
                        Trace.TraceWarning("Duplicate key, retrying...");
                        ApplicationInsights.TrackSqlException("SQL Duplicate Key", e, logFileName, dimension);
                    }
                    else
                    {
                        throw;
                    }

                    Task.Delay(_retryDelay).Wait();
                }
                catch (Exception exception)
                {
                    _jobEventSource.FailedRetrieveDimension(dimension);
                    ApplicationInsights.TrackException(exception, logFileName);

                    if (stopwatch.IsRunning)
                        stopwatch.Stop();

                    throw;
                }
            }
            return Enumerable.Empty<T>().ToList();
        }

        private static void FillDataRow(DataRow dataRow, int dateId, int timeId, int packageId, int operationId, int platformId, int projectTypeId, int clientId, int userAgentId, int logFileNameId, int edgeServerIpAddressId)
        {
            dataRow["Id"] = Guid.NewGuid();
            dataRow["Dimension_Package_Id"] = packageId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Operation_Id"] = operationId;
            dataRow["Dimension_ProjectType_Id"] = projectTypeId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
            dataRow["Fact_UserAgent_Id"] = userAgentId;
            dataRow["Fact_LogFileName_Id"] = logFileNameId;
            dataRow["Fact_EdgeServer_IpAddress_Id"] = edgeServerIpAddressId;
            dataRow["DownloadCount"] = 1;
        }

        private static void FillToolDataRow(DataRow dataRow, int dateId, int timeId, int toolId, int platformId, int clientId, int userAgentId, int logFileNameId, int edgeServerIpAddressId)
        {
            dataRow["Id"] = Guid.NewGuid();
            dataRow["Dimension_Tool_Id"] = toolId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
            dataRow["Fact_UserAgent_Id"] = userAgentId;
            dataRow["Fact_LogFileName_Id"] = logFileNameId;
            dataRow["Fact_EdgeServer_IpAddress_Id"] = edgeServerIpAddressId;
            dataRow["DownloadCount"] = 1;
        }

        private static async Task<IReadOnlyCollection<ToolDimension>> RetrieveToolDimensions(IReadOnlyCollection<ToolStatistics> sourceData, SqlConnection connection)
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

            results.AddRange(_cachedToolDimensions
                .Where(p1 => tools
                    .FirstOrDefault(p2 =>
                        string.Equals(p1.ToolId, p2.ToolId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p1.ToolVersion, p2.ToolVersion, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p1.FileName, p2.FileName, StringComparison.OrdinalIgnoreCase)) != null
                    )
                );

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
                        var tool = new ToolDimension(dataReader.GetString(1), dataReader.GetString(2), dataReader.GetString(3));
                        tool.Id = dataReader.GetInt32(0);

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

        private static async Task<IReadOnlyCollection<PackageDimension>> RetrievePackageDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
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

            results.AddRange(_cachedPackageDimensions
                .Where(p1 => packages
                    .FirstOrDefault(p2 =>
                        string.Equals(p1.PackageId, p2.PackageId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p1.PackageVersion, p2.PackageVersion, StringComparison.OrdinalIgnoreCase)) != null
                    )
                );

            var nonCachedPackageDimensions = packages.Except(results).ToList();

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
                        var package = new PackageDimension(dataReader.GetString(1), dataReader.GetString(2));
                        package.Id = dataReader.GetInt32(0);

                        if (!results.Contains(package))
                        {
                            results.Add(package);
                        }
                        if (!_cachedPackageDimensions.Contains(package))
                        {
                            _cachedPackageDimensions.Add(package);
                        }
                    }
                }
            }

            return results;
        }

        private static async Task<IReadOnlyCollection<PackageTranslation>> RetrievePackageTranslations(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[GetPackageTranslations]";
            command.CommandTimeout = _defaultCommandTimeout;
            command.CommandType = CommandType.StoredProcedure;

            var results = new List<PackageTranslation>();
            using (var dataReader = await command.ExecuteReaderAsync())
            {
                while (await dataReader.ReadAsync())
                {
                    var packageTranslation = new PackageTranslation();
                    packageTranslation.CorrectedPackageId = dataReader.GetInt32(0);
                    packageTranslation.IncorrectPackageId = dataReader.GetString(1);
                    packageTranslation.IncorrectPackageVersion = dataReader.GetString(2);

                    results.Add(packageTranslation);
                }
            }

            return results;
        }

        private static async Task<IReadOnlyCollection<DateDimension>> RetrieveDateDimensions(SqlConnection connection, DateTime min, DateTime max)
        {
            var results = new List<DateDimension>();

            var command = connection.CreateCommand();
            command.CommandText = SqlQueries.GetDateDimensions(min, max);
            command.CommandTimeout = _defaultCommandTimeout;
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

        private static async Task<IReadOnlyCollection<TimeDimension>> RetrieveTimeDimensions(SqlConnection connection)
        {
            var results = new List<TimeDimension>();

            var command = connection.CreateCommand();
            command.CommandText = SqlQueries.GetAllTimeDimensions();
            command.CommandTimeout = _defaultCommandTimeout;
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

        private static async Task<IDictionary<string, int>> RetrieveOperationDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
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

        private static async Task<IDictionary<string, int>> RetrieveProjectTypeDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
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

            var nonCachedProjectTypes = new List<string>();
            foreach (var projectType in projectTypes)
            {
                if (_cachedProjectTypeDimensions.ContainsKey(projectType))
                {
                    var cachedProjectTypeId = _cachedProjectTypeDimensions[projectType];
                    results.Add(projectType, cachedProjectTypeId);
                }
                else
                {
                    nonCachedProjectTypes.Add(projectType);
                }
            }

            if (nonCachedProjectTypes.Any())
            {
                var projectTypesParameter = string.Join(",", nonCachedProjectTypes);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsureProjectTypeDimensionsExist]";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = _defaultCommandTimeout;
                command.Parameters.AddWithValue("projectTypes", projectTypesParameter);

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var projectType = dataReader.GetString(1);
                        var projectTypeId = dataReader.GetInt32(0);
                        if (!_cachedProjectTypeDimensions.ContainsKey(projectType))
                        {
                            _cachedProjectTypeDimensions.Add(projectType, projectTypeId);
                        }
                        results.Add(projectType, projectTypeId);
                    }
                }
            }

            return results;
        }

        private static async Task<IDictionary<string, int>> RetrieveClientDimensions(IReadOnlyCollection<ITrackUserAgent> sourceData, SqlConnection connection)
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

        private static async Task<IDictionary<string, int>> RetrievePlatformDimensions(IReadOnlyCollection<ITrackUserAgent> sourceData, SqlConnection connection)
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

        private static async Task<IDictionary<string, int>> RetrieveUserAgentFacts(IReadOnlyCollection<ITrackUserAgent> sourceData, SqlConnection connection)
        {
            var userAgents = sourceData
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent)
                .Select(e => e.First())
                .Select(e => e.UserAgent)
                .ToList();

            var results = new Dictionary<string, int>();
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
                else
                {
                    nonCachedUserAgents.Add(userAgent);
                }
            }

            if (nonCachedUserAgents.Any())
            {
                var parameterValue = UserAgentFactTableType.CreateDataTable(nonCachedUserAgents);

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

                        results.Add(userAgent, userAgentId);
                    }
                }
            }

            return results;
        }

        private static async Task<IDictionary<string, int>> RetrieveLogFileNameFacts(string logFileName, SqlConnection connection)
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

        private static async Task<IDictionary<string, int>> RetrieveIpAddressesFacts(IReadOnlyCollection<ITrackEdgeServerIpAddress> sourceData, SqlConnection connection)
        {
            var ipAddressFacts = sourceData
                .Where(e => !string.IsNullOrEmpty(e.EdgeServerIpAddress))
                .GroupBy(e => e.EdgeServerIpAddress)
                .Select(e => e.First())
                .ToDictionary(e => e.EdgeServerIpAddress, e => new IpAddressFact(e.EdgeServerIpAddress));

            var results = new Dictionary<string, int>();
            if (!ipAddressFacts.Any())
            {
                return results;
            }

            var nonCachedIpAddresses = new Dictionary<string, IpAddressFact>();
            foreach (var ipAddressFact in ipAddressFacts)
            {
                if (_cachedIpAddressFacts.ContainsKey(ipAddressFact.Key))
                {
                    var cachedUserAgentFactId = _cachedIpAddressFacts[ipAddressFact.Key];
                    results.Add(ipAddressFact.Key, cachedUserAgentFactId);
                }
                else
                {
                    nonCachedIpAddresses.Add(ipAddressFact.Key, ipAddressFact.Value);
                }
            }

            if (nonCachedIpAddresses.Any())
            {
                var parameterValue = CreateDataTable(nonCachedIpAddresses);

                var command = connection.CreateCommand();
                command.CommandText = "[dbo].[EnsureIpAddressFactsExist]";
                command.CommandTimeout = _defaultCommandTimeout;
                command.CommandType = CommandType.StoredProcedure;

                var parameter = command.Parameters.AddWithValue("addresses", parameterValue);
                parameter.SqlDbType = SqlDbType.Structured;
                parameter.TypeName = "[dbo].[IpAddressFactTableType]";

                using (var dataReader = await command.ExecuteReaderAsync())
                {
                    while (await dataReader.ReadAsync())
                    {
                        var ipAddress = dataReader.GetString(1);
                        var ipAddressId = dataReader.GetInt32(0);

                        if (!_cachedIpAddressFacts.ContainsKey(ipAddress))
                        {
                            _cachedIpAddressFacts.Add(ipAddress, ipAddressId);
                        }

                        results.Add(ipAddress, ipAddressId);
                    }
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
            table.Columns.Add("ToolId", typeof (string));
            table.Columns.Add("ToolVersion", typeof (string));
            table.Columns.Add("FileName", typeof (string));

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
    }
}