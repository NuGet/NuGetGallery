// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;

namespace Stats.AggregateInTableStorage
{
    internal class Job
        : JobBase
    {
        private PackageStatisticsTable _sourceTable;
        private AggregatePackageStatisticsTable _targetTable;
        private PackageStatisticsQueue _messageQueue;

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
                _targetTable = new AggregatePackageStatisticsTable(cloudStorageAccount);

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

                await _targetTable.CreateIfNotExistsAsync();
                await _messageQueue.CreateIfNotExists();

                PackageStatisticsQueueMessage message = await _messageQueue.GetMessageAsync();

                var traceMessage = string.Format("Start processing message with ID: {0} (elements count = [{1}].", message.Id, message.PartitionAndRowKeys.Count);
                Trace.WriteLine(traceMessage);

                if (message != null)
                {
                    await StatisticsAggregator.AggregateTotalDownloadCounts(_sourceTable, _targetTable, message);

                    await _messageQueue.DeleteMessage(message);
                }

                stopwatch.Stop();
                traceMessage = string.Format("Execution time: {0} ms.", stopwatch.ElapsedMilliseconds);
                Trace.WriteLine(traceMessage);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
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