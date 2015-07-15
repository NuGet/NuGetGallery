// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;

namespace Stats.AggregateDownloadsInTempTable
{
    internal class Job
        : JobBase
    {
        private PackageStatisticsTable _sourceTable;
        private PackageStatisticsQueue _messageQueue;
        private TemporaryPackageDownloadStatisticsTable _tempAggregationTable;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var cloudStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                var cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString);

                _messageQueue = new PackageStatisticsQueue(cloudStorageAccount);
                _sourceTable = new PackageStatisticsTable(cloudStorageAccount);
                _tempAggregationTable = new TemporaryPackageDownloadStatisticsTable(cloudStorageAccount);

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
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                await _tempAggregationTable.CreateIfNotExistsAsync();
                await _messageQueue.CreateIfNotExists();

                var tasks = new List<Task>();
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    tasks.Add(Task.Run(async () => await AggregateDownloadStatisticsAsync(cancellationTokenSource), cancellationTokenSource.Token));
                }

                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception exception)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource.Cancel();
                }

                Trace.TraceError(exception.ToString());
            }
            return false;
        }

        private async Task AggregateDownloadStatisticsAsync(CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                var statisticsAggregator = new StatisticsAggregator(_sourceTable, _tempAggregationTable);

                var stopwatch = Stopwatch.StartNew();
                IReadOnlyCollection<PackageStatisticsQueueMessage> messages = await _messageQueue.GetMessagesAsync();

                await statisticsAggregator.AggregateTotalDownloadCounts(messages);
                await _messageQueue.DeleteMessages(messages);

                stopwatch.Stop();
                Trace.WriteLine(string.Format("[{0}] Execution time: {1} ms.", statisticsAggregator.AggregatorId, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception exception)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource.Cancel();
                }

                Trace.TraceError(exception.ToString());
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