// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;

namespace Stats.AggregateDownloadsInTempTable
{
    internal class StatisticsAggregator
    {
        private readonly PackageStatisticsTable _sourceTable;
        private readonly TemporaryPackageDownloadStatisticsTable _targetTable;

        public StatisticsAggregator(PackageStatisticsTable sourceTable, TemporaryPackageDownloadStatisticsTable targetTable)
        {
            _sourceTable = sourceTable;
            _targetTable = targetTable;

            var currentProcess = Process.GetCurrentProcess();
            AggregatorId = $"{Environment.MachineName}::{currentProcess.ProcessName}({currentProcess.Id})::{Thread.CurrentThread.ManagedThreadId}";
        }

        public string AggregatorId { get; }

        public async Task AggregateTotalDownloadCounts(IReadOnlyCollection<PackageStatisticsQueueMessage> messages)
        {
            // Get batch of statistics to be processed
            var batch = _sourceTable.GetNextAggregationBatch(messages);
            if (batch.Count == 0)
            {
                return;
            }

            // The batch size is max 1000 entities, so no need to worry about max batch size anymore.
            var groupedByPackageId = batch.GroupBy(e => e.PackageId).ToList();

            var temporaryAggregateStats = new List<TemporaryPackageDownloadStatistic>();
            foreach (var packageIdGroup in groupedByPackageId)
            {
                // As the package ID is the partition key, we can do batch operations on table storage for these records.
                var packageId = packageIdGroup.Key;
                var groupedByPackageIdAndVersion = packageIdGroup.GroupBy(e => e.PackageVersion);
                foreach (var packageIdAndVersionGroup in groupedByPackageIdAndVersion)
                {
                    // aggregated on package id and version
                    var packageIdAndVersionStatistic = new TemporaryPackageDownloadStatistic();
                    packageIdAndVersionStatistic.AggregatorId = AggregatorId;
                    packageIdAndVersionStatistic.PackageId = packageId;
                    packageIdAndVersionStatistic.PackageVersion = packageIdAndVersionGroup.Key;
                    packageIdAndVersionStatistic.TotalDownloadCount = packageIdAndVersionGroup.Count();

                    temporaryAggregateStats.Add(packageIdAndVersionStatistic);
                }
            }

            // The insert-or-replace-batch methods below will do safe and optimal batching based on partition key and operation count
            await _targetTable.InsertOrReplaceBatchAsync(temporaryAggregateStats);
        }
    }
}