// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class Job
        : JobBase
    {
        private PackageStatisticsTable _sourceTable;
        private PackageStatisticsQueue _messageQueue;
        private SqlConnectionStringBuilder _targetDatabase;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
                var cloudStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                var cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString);

                _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
                _sourceTable = new PackageStatisticsTable(cloudStorageAccount);
                _messageQueue = new PackageStatisticsQueue(cloudStorageAccount);

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                await _sourceTable.CreateIfNotExistsAsync();
                await _messageQueue.CreateIfNotExists();

                // get next batch of elements to be processed
                Trace.WriteLine("Fetching messages from the queue...");
                var messages = await _messageQueue.GetMessagesAsync();
                Trace.Write("  DONE (" + messages.Count + " messages)");

                Trace.WriteLine("Fetching raw records for aggregation...");
                var sourceData = _sourceTable.GetNextAggregationBatch(messages);
                Trace.Write("  DONE (" + sourceData.Count + " records)");

                // replicate data to the statistics database
                using (var connection = await _targetDatabase.ConnectTo())
                {
                    var facts = await CreateFactsAsync(sourceData, connection);

                    await InsertDownloadFactsAsync(facts, connection);
                }

                // delete messages from the queue
                Trace.WriteLine("Deleting processed messages from queue...");
                await _messageQueue.DeleteMessagesAsync(messages);
                Trace.Write("  DONE");

                stopwatch.Stop();
                Trace.WriteLine("Time elapsed: " + stopwatch.Elapsed);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }

        private static async Task<DataTable> CreateFactsAsync(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var stopwatch = Stopwatch.StartNew();

            // insert any new dimension data first
            Trace.WriteLine("Querying dimension: operation");
            var operations = await RetrieveOperationDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + operations.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: project type");
            stopwatch.Restart();
            var projectTypes = await RetrieveProjectTypeDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + projectTypes.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: client");
            stopwatch.Restart();
            var clients = await RetrieveClientDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + clients.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: platform");
            stopwatch.Restart();
            var platforms = await RetrievePlatformDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + platforms.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: time");
            stopwatch.Restart();
            var times = await RetrieveTimeDimensions(connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + times.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: date");
            stopwatch.Restart();
            var dates = await RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered));
            stopwatch.Stop();
            Trace.Write("  DONE (" + dates.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            Trace.WriteLine("Querying dimension: package");
            stopwatch.Restart();
            var packages = await RetrievePackageDimensions(sourceData, connection);
            stopwatch.Stop();
            Trace.Write("  DONE (" + packages.Count + " objects, " + stopwatch.ElapsedMilliseconds + "ms)");

            // create facts data rows by linking source data with dimensions
            // insert into temp table for increased scalability and allow for aggregation later

            var dataTable = DataImporter.GetDataTable("Fact_Download", connection);

            // ensure all dimension IDs are set to the Unknown equivalent if no dimension data is available
            int? operationId = !operations.Any() ? DimensionId.Unknown : (int?)null;
            int? projectTypeId = !projectTypes.Any() ? DimensionId.Unknown : (int?)null;
            int? clientId = !clients.Any() ? DimensionId.Unknown : (int?)null;
            int? platformId = !platforms.Any() ? DimensionId.Unknown : (int?)null;

            Trace.WriteLine("Creating facts...");
            stopwatch.Restart();
            foreach (var groupedByPackageId in sourceData.GroupBy(e => e.PackageId))
            {
                var packagesForId = packages.Where(e => e.PackageId == groupedByPackageId.Key).ToList();

                foreach (var groupedByPackageIdAndVersion in groupedByPackageId.GroupBy(e => e.PackageVersion))
                {
                    var packageId = packagesForId.First(e => e.PackageVersion == groupedByPackageIdAndVersion.Key).Id;

                    foreach (var element in groupedByPackageIdAndVersion)
                    {
                        // required dimensions
                        var dateId = dates.First(e => e.Date.Equals(element.EdgeServerTimeDelivered.Date)).Id;
                        var timeId = times.First(e => e.HourOfDay == element.EdgeServerTimeDelivered.Hour).Id;

                        // dimensions that could be "(unknown)"
                        if (!operationId.HasValue)
                        {
                            operationId = operations[element.Operation];
                        }
                        if (!platformId.HasValue)
                        {
                            platformId = platforms[element.UserAgent];
                        }
                        if (!clientId.HasValue)
                        {
                            clientId = clients[element.UserAgent];
                        }

                        if (!projectTypeId.HasValue)
                        {
                            // foreach project type
                            foreach (var projectGuid in element.ProjectGuids.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                projectTypeId = projectTypes[projectGuid];

                                var dataRow = dataTable.NewRow();
                                FillDataRow(dataRow, dateId, timeId, packageId, operationId.Value, platformId.Value, projectTypeId.Value, clientId.Value);
                                dataTable.Rows.Add(dataRow);
                            }
                        }
                        else
                        {
                            var dataRow = dataTable.NewRow();
                            FillDataRow(dataRow, dateId, timeId, packageId, operationId.Value, platformId.Value, projectTypeId.Value, clientId.Value);
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }
            stopwatch.Stop();
            Trace.Write("  DONE (" + dataTable.Rows.Count + " records, " + stopwatch.ElapsedMilliseconds + "ms)");

            return dataTable;
        }

        private static void FillDataRow(DataRow dataRow, int dateId, int timeId, int packageId, int operationId, int platformId, int projectTypeId, int clientId)
        {
            dataRow["Id"] = Guid.NewGuid();
            dataRow["Dimension_Package_Id"] = packageId;
            dataRow["Dimension_Date_Id"] = dateId;
            dataRow["Dimension_Time_Id"] = timeId;
            dataRow["Dimension_Operation_Id"] = operationId;
            dataRow["Dimension_ProjectType_Id"] = projectTypeId;
            dataRow["Dimension_Client_Id"] = clientId;
            dataRow["Dimension_Platform_Id"] = platformId;
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

        private static async Task<IReadOnlyCollection<DateDimension>> RetrieveDateDimensions(SqlConnection connection, DateTime min, DateTime max)
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

        private static async Task<IReadOnlyCollection<TimeDimension>> RetrieveTimeDimensions(SqlConnection connection)
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

        private static async Task InsertDownloadFactsAsync(DataTable facts, SqlConnection connection)
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

        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }
            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
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