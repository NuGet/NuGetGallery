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

                var traceMessage = string.Format("Fetched {0} messages.", messages.Count);
                Trace.WriteLine(traceMessage);

                Trace.WriteLine("Fetching raw records for aggregation...");
                var sourceData = _sourceTable.GetNextAggregationBatch(messages);

                traceMessage = string.Format("Fetched {0} raw package download records...", sourceData.Count);
                Trace.WriteLine(traceMessage);

                // replicate data to the statistics database
                using (var connection = await _targetDatabase.ConnectTo())
                {
                    var facts = await CreateFactsAsync(sourceData, connection);

                    await InsertDownloadFacts(facts, connection);
                }

                // delete messages from the queue
                await _messageQueue.DeleteMessages(messages);

                stopwatch.Stop();
                traceMessage = string.Format("Time elapsed: {0}", stopwatch.Elapsed);
                Trace.WriteLine(traceMessage);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }

        private static async Task<DataRow[]> CreateFactsAsync(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            // insert any new dimension data first
            var operations = await InsertOperationDimensions(sourceData, connection);
            var projectTypes = await InsertProjectTypeDimensions(sourceData, connection);
            var clients = await InsertClientDimensions(sourceData, connection);
            var clientPlatforms = await InsertClientPlatformDimensions(sourceData, connection);
            var times = await GetAllTimeDimensions(connection);
            var dates = await GetDateDimensions(connection, sourceData.Min(e => e.EdgeServerTimeDelivered), sourceData.Max(e => e.EdgeServerTimeDelivered));

            // create facts data rows by linking source data with dimensions
            // todo: continue here :)

            return null;
        }

        private static async Task<IReadOnlyCollection<DateDimension>> GetDateDimensions(SqlConnection connection, DateTime min, DateTime max)
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

        private static async Task<IReadOnlyCollection<TimeDimension>> GetAllTimeDimensions(SqlConnection connection)
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

        private static async Task<IDictionary<int, string>> InsertOperationDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var operations = sourceData.Select(e => e.Operation).Distinct().ToList();
            if (operations.Count == 0)
            {
                // set operation to be (unknown)
                operations.Add("(unknown)");
            }

            var results = new Dictionary<int, string>();

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
                        results.Add(id, parameter);
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

        private static async Task<IDictionary<int, string>> InsertProjectTypeDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var projectTypes = sourceData.SelectMany(e => e.ProjectGuids.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries)).Distinct().ToList();
            if (projectTypes.Count == 0)
            {
                // add the (unknown) operation
                projectTypes.Add("(unknown)");
            }

            var results = new Dictionary<int, string>();

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
                        results.Add(id, parameter);
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

        private static async Task<IReadOnlyCollection<ClientDimension>> InsertClientDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var clientDimensions = sourceData.Select(ClientDimension.FromPackageStatistic).Distinct().ToList();
            if (clientDimensions.Count == 0)
            {
                // add the (unknown) operation
                clientDimensions.Add(ClientDimension.Unknown);
            }

            var results = new List<ClientDimension>();
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var clientDimension in clientDimensions)
                    {
                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetClientDimensionAndCreateIfNotExists(clientDimension);
                        command.CommandType = CommandType.Text;

                        clientDimension.Id = (int)await command.ExecuteScalarAsync();

                        results.Add(clientDimension);
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

        private static async Task<IReadOnlyCollection<ClientPlatformDimension>> InsertClientPlatformDimensions(IReadOnlyCollection<PackageStatistics> sourceData, SqlConnection connection)
        {
            var platformDimensions = sourceData.Select(ClientPlatformDimension.FromPackageStatistic).Distinct().ToList();
            if (platformDimensions.Count == 0)
            {
                // add the (unknown) operation
                platformDimensions.Add(ClientPlatformDimension.Unknown);
            }

            var results = new List<ClientPlatformDimension>();
            using (var transaction = connection.BeginTransaction(IsolationLevel.Snapshot))
            {
                try
                {
                    foreach (var platformDimension in platformDimensions)
                    {
                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = SqlQueries.GetPlatformDimensionAndCreateIfNotExists(platformDimension);
                        command.CommandType = CommandType.Text;

                        platformDimension.Id = (int)await command.ExecuteScalarAsync();

                        results.Add(platformDimension);
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

        private static async Task InsertDownloadFacts(DataRow[] facts, SqlConnection connection)
        {
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
            {
                bulkCopy.BatchSize = facts.Length;
                bulkCopy.DestinationTableName = "[Fact.Download]";
                connection.Open();
                await bulkCopy.WriteToServerAsync(facts);
            }
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