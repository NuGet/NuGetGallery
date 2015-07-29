// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
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
                IReadOnlyCollection<PackageStatistics> sourceData = _sourceTable.GetNextAggregationBatch(messages);
                Trace.Write("  DONE (" + sourceData.Count + " records)");

                // replicate data to the statistics database
                using (var connection = await _targetDatabase.ConnectTo())
                {
                    var facts = await DownloadFacts.CreateAsync(sourceData, connection);

                    await Warehouse.InsertDownloadFactsAsync(facts, connection);
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