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
        private const int _defaultCommandTimeout = 420; // 7 minutes max
        private const int _maxRetryCount = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);
        private readonly JobEventSource _jobEventSource;
        private readonly SqlConnectionStringBuilder _targetDatabase;

        public Warehouse(JobEventSource jobEventSource, SqlConnectionStringBuilder targetDatabase)
        {
            _jobEventSource = jobEventSource;
            _targetDatabase = targetDatabase;
        }

        internal async Task InsertDownloadFactsAsync(IReadOnlyCollection<PackageStatistics> packageStatistics, string logFileName)
        {
            var downloadFacts = await CreateAsync(packageStatistics, logFileName);
            ApplicationInsights.TrackMetric("Blob record count", downloadFacts.Rows.Count, logFileName);

            Trace.WriteLine("Inserting into facts table...");
            var stopwatch = Stopwatch.StartNew();

            using (var connection = await _targetDatabase.ConnectTo())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
                bulkCopy.BatchSize = downloadFacts.Rows.Count;
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

        private async Task<DataTable> CreateAsync(IReadOnlyCollection<PackageStatistics> sourceData, string logFileName)
        {
            var stopwatch = Stopwatch.StartNew();

            // insert any new dimension data first
            var operationsTask = GetDimension("operation", logFileName, connection => RetrieveOperationDimensions(sourceData, connection));
            var projectTypesTask = GetDimension("project type", logFileName, connection => RetrieveProjectTypeDimensions(sourceData, connection));
            var clientsTask = GetDimension("client", logFileName, connection => RetrieveClientDimensions(sourceData, connection));
            var platformsTask = GetDimension("platform", logFileName, connection => RetrievePlatformDimensions(sourceData, connection));
            var timesTask = GetDimension("time", logFileName, connection => RetrieveTimeDimensions(connection));
            var datesTask = GetDimension("date", logFileName, connection => RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered)));
            var packagesTask = GetDimension("package", logFileName, connection => RetrievePackageDimensions(sourceData, connection));
            var packageTranslationsTask = GetDimension("package translations", logFileName, connection => RetrievePackageTranslations(sourceData, connection));

            // create facts data rows by linking source data with dimensions
            // insert into temp table for increased scalability and allow for aggregation later

            await Task.WhenAll(operationsTask, projectTypesTask, clientsTask, platformsTask, timesTask, datesTask, packagesTask, packageTranslationsTask);

            var operations = operationsTask.Result;
            var projectTypes = projectTypesTask.Result;
            var clients = clientsTask.Result;
            var platforms = platformsTask.Result;
            var times = timesTask.Result;
            var dates = datesTask.Result;
            var packages = packagesTask.Result;
            var packageTranslations = packageTranslationsTask.Result;

            var dataImporter = new DataImporter(_targetDatabase);
            var dataTable = await dataImporter.GetDataTableAsync("Fact_Download");

            // ensure all dimension IDs are set to the Unknown equivalent if no dimension data is available
            int? operationId = !operations.Any() ? DimensionId.Unknown : (int?)null;
            int? projectTypeId = !projectTypes.Any() ? DimensionId.Unknown : (int?)null;
            int? clientId = !clients.Any() ? DimensionId.Unknown : (int?)null;
            int? platformId = !platforms.Any() ? DimensionId.Unknown : (int?)null;

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
                        var timeId = times.First(e => e.HourOfDay == element.EdgeServerTimeDelivered.Hour).Id;

                        // dimensions that could be "(unknown)"
                        if (!operationId.HasValue)
                        {
                            if (!operations.ContainsKey(element.Operation))
                            {
                                operationId = DimensionId.Unknown;
                            }
                            else
                            {
                                operationId = operations[element.Operation];
                            }
                        }
                        if (!platformId.HasValue)
                        {
                            if (!platforms.ContainsKey(element.UserAgent))
                            {
                                platformId = DimensionId.Unknown;
                            }
                            else
                            {
                                platformId = platforms[element.UserAgent];
                            }
                        }
                        if (!clientId.HasValue)
                        {
                            if (!clients.ContainsKey(element.UserAgent))
                            {
                                clientId = DimensionId.Unknown;
                            }
                            else
                            {
                                clientId = clients[element.UserAgent];
                            }
                        }

                        if (!projectTypeId.HasValue)
                        {
                            // foreach project type
                            foreach (var projectGuid in element.ProjectGuids.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                projectTypeId = projectTypes[projectGuid];

                                var dataRow = dataTable.NewRow();
                                FillDataRow(dataRow, dateId, timeId, packageId, operationId.Value, platformId.Value, projectTypeId.Value, clientId.Value, logFileName, element.UserAgent);
                                dataTable.Rows.Add(dataRow);
                            }
                        }
                        else
                        {
                            var dataRow = dataTable.NewRow();
                            FillDataRow(dataRow, dateId, timeId, packageId, operationId.Value, platformId.Value, projectTypeId.Value, clientId.Value, logFileName, element.UserAgent);
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

        private static void FillDataRow(DataRow dataRow, int dateId, int timeId, int packageId, int operationId, int platformId, int projectTypeId, int clientId, string logFileName, string userAgent)
        {
            dataRow["Id"] = Guid.NewGuid();
            dataRow["Dimension_Package_Id"] = packageId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Operation_Id"] = operationId;
            dataRow["Dimension_ProjectType_Id"] = projectTypeId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
            dataRow["LogFileName"] = logFileName;
            dataRow["UserAgent"] = userAgent;
            dataRow["DownloadCount"] = 1;
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

            var parameterValue = CreateDataTable(packages);

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
                        results.Add(package);
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

            var operationsParameter = string.Join(",", operations);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureOperationDimensionsExist]";
            command.CommandTimeout = _defaultCommandTimeout;
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

            var projectTypesParameter = string.Join(",", projectTypes);

            var command = connection.CreateCommand();
            command.CommandText = "[dbo].[EnsureProjectTypeDimensionsExist]";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _defaultCommandTimeout;
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

        private static async Task<IDictionary<string, int>> RetrieveClientDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
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
            command.CommandTimeout = _defaultCommandTimeout;

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

        private static async Task<IDictionary<string, int>> RetrievePlatformDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
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
            command.CommandTimeout = _defaultCommandTimeout;
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

        private static DataTable CreateDataTable(Dictionary<string, ClientDimension> clientDimensions)
        {
            var table = new DataTable();
            table.Columns.Add("UserAgent", typeof(string));
            table.Columns.Add("ClientName", typeof(string));
            table.Columns.Add("Major", typeof(string));
            table.Columns.Add("Minor", typeof(string));
            table.Columns.Add("Patch", typeof(string));

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