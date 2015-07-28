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
                Trace.Write("  DONE (" + messages.Count +" messages)");

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
            // insert any new dimension data first
            Trace.WriteLine("Retrieving operation dimensions...");
            var operations = await RetrieveOperationDimensions(sourceData, connection);
            Trace.Write("  DONE (" + operations.Count + " operations)");

            Trace.WriteLine("Retrieving project type dimensions...");
            var projectTypes = await RetrieveProjectTypeDimensions(sourceData, connection);
            Trace.Write("  DONE (" + projectTypes.Count + " project types)");

            Trace.WriteLine("Retrieving client dimensions...");
            var clients = await RetrieveClientDimensions(sourceData, connection);
            Trace.Write("  DONE (" + clients.Count + " clients)");

            Trace.WriteLine("Retrieving platform dimensions...");
            var platforms = await RetrievePlatformDimensions(sourceData, connection);
            Trace.Write("  DONE (" + platforms.Count + " platforms)");

            Trace.WriteLine("Retrieving time dimensions...");
            var times = await RetrieveTimeDimensions(connection);
            Trace.Write("  DONE (" + times.Count + " times)");

            Trace.WriteLine("Retrieving date dimensions...");
            var dates = await RetrieveDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered));
            Trace.Write("  DONE (" + dates.Count + " dates)");

            Trace.WriteLine("Retrieving package dimensions...");
            var packages = await RetrievePackageDimensions(sourceData, connection);
            Trace.Write("  DONE (" + packages.Count + " packages)");

            // create facts data rows by linking source data with dimensions
            // insert into temp table for increased scalability and allow for aggregation later

            var dataTable = DataImporter.GetDataTable("Temp_Fact_Download", connection);

            // ensure all dimension IDs are set to the Unknown equivalent if no dimension data is available
            var operationId = !operations.Any() ? DimensionId.Unknown : 0;
            var projectTypeId = !projectTypes.Any() ? DimensionId.Unknown : 0;
            var clientId = !clients.Any() ? DimensionId.Unknown : 0;
            var platformId = !platforms.Any() ? DimensionId.Unknown : 0;

            Trace.WriteLine("Creating facts...");
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
                        if (operationId == 0)
                        {
                            operationId = operations[element.Operation];
                        }
                        if (platformId == 0)
                        {
                            platformId = platforms[element.UserAgent];
                        }
                        if (clientId == 0)
                        {
                            clientId = clients[element.UserAgent];
                        }

                        if (projectTypeId != DimensionId.Unknown)
                        {
                            // foreach project type
                            foreach (var projectGuid in element.ProjectGuids.Split(new[] {";"}, StringSplitOptions.RemoveEmptyEntries))
                            {
                                projectTypeId = projectTypes[projectGuid];

                                var dataRow = dataTable.NewRow();
                                FillDataRow(dataRow, dateId, timeId, packageId, operationId, platformId, projectTypeId, clientId);
                                dataTable.Rows.Add(dataRow);
                            }
                        }
                        else
                        {
                            var dataRow = dataTable.NewRow();
                            FillDataRow(dataRow, dateId, timeId, packageId, operationId, platformId, projectTypeId, clientId);
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }

            Trace.Write("  DONE (" + dataTable.Rows.Count + " records)");

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

            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var package in packages)
                    {
                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetPackageDimensionAndCreateIfNotExists(package);
                        command.CommandType = CommandType.Text;

                        package.Id = (int)await command.ExecuteScalarAsync();

                        if (!results.Contains(package))
                            results.Add(package);
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                    transaction.Rollback();

                    throw;
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

            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var operation in operations)
                    {
                        string parameter = operation;
                        if (string.IsNullOrEmpty(operation))
                        {
                            parameter = "(unknown)";
                        }

                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetOperationDimensionAndCreateIfNotExists(parameter);
                        command.CommandType = CommandType.Text;
                        var id = (int)await command.ExecuteScalarAsync();
                        results.Add(parameter, id);
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                    transaction.Rollback();

                    throw;
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

            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var projectType in projectTypes)
                    {
                        string parameter = projectType;
                        if (string.IsNullOrEmpty(projectType))
                        {
                            parameter = "(unknown)";
                        }

                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetProjectTypeDimensionAndCreateIfNotExists(parameter);
                        command.CommandType = CommandType.Text;
                        var id = (int)await command.ExecuteScalarAsync();
                        results.Add(parameter, id);
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                    transaction.Rollback();

                    throw;
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

            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var clientDimension in clientDimensions)
                    {
                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetClientDimensionAndCreateIfNotExists(clientDimension.Value);
                        command.CommandType = CommandType.Text;

                        clientDimension.Value.Id = (int)await command.ExecuteScalarAsync();

                        results.Add(clientDimension.Key, clientDimension.Value.Id);
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                    transaction.Rollback();

                    throw;
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

            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var platformDimension in platformDimensions)
                    {
                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetPlatformDimensionAndCreateIfNotExists(platformDimension.Value);
                        command.CommandType = CommandType.Text;

                        platformDimension.Value.Id = (int)await command.ExecuteScalarAsync();

                        results.Add(platformDimension.Key, platformDimension.Value.Id);
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                    transaction.Rollback();

                    throw;
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
                bulkCopy.DestinationTableName = "Temp_Fact_Download";

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
    }
}