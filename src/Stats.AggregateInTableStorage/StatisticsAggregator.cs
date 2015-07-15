// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common;

namespace Stats.AggregateInTableStorage
{
    internal static class StatisticsAggregator
    {
        public static async Task AggregateTotalDownloadCounts(PackageStatisticsTable sourceTable, AggregatePackageStatisticsTable targetTable, PackageStatisticsQueueMessage message)
        {
            // Get batch of statistics to be processed
            var batch = sourceTable.GetNextAggregationBatch(message);
            if (batch.Count == 0)
            {
                return;
            }

            // The batch size is max 250 entities, so no need to worry about max batch size anymore.
            var groupedByPackageId = batch.GroupBy(e => e.PackageId).ToList();

            // Get existing aggregate package statistics
            var existingPackageIdStatistics = targetTable.GetAggregatePackageStatistics(groupedByPackageId.Select(e => e.Key));


            var statisticsToInsertOrReplace = new List<PackageDownloadStatistic>();
            foreach (var packageIdGroup in groupedByPackageId)
            {
                // Get existing statistics data for package ID's and versions.
                // As the package ID is the partition key, we can do batch operations on table storage for these records.
                var partitionKey = packageIdGroup.Key;

                // aggregated on package id
                var existingPackageIdStatistic = existingPackageIdStatistics.FirstOrDefault(e => e.PackageId == partitionKey && e.PackageVersion == string.Empty);

                var packageIdStatistic = new PackageDownloadStatistic();
                packageIdStatistic.PackageId = partitionKey;
                packageIdStatistic.PackageVersion = string.Empty;

                // add existing total download count and new download count when applicable
                var downloadCount = existingPackageIdStatistic == null ? packageIdGroup.Count() : (existingPackageIdStatistic.TotalDownloadCount + packageIdGroup.Count());
                packageIdStatistic.TotalDownloadCount = downloadCount;

                statisticsToInsertOrReplace.Add(packageIdStatistic);

                var groupedByPackageIdAndVersion = packageIdGroup.GroupBy(e => e.PackageVersion);
                foreach (var packageIdAndVersionGroup in groupedByPackageIdAndVersion)
                {
                    var existingPackageIdAndVersionStatistic = existingPackageIdStatistics.FirstOrDefault(e => e.PackageId == partitionKey && e.PackageVersion == packageIdAndVersionGroup.Key);

                    // aggregated on package id and version
                    var packageIdAndVersionStatistic = new PackageDownloadStatistic();
                    packageIdAndVersionStatistic.PackageId = partitionKey;
                    packageIdAndVersionStatistic.PackageVersion = packageIdAndVersionGroup.Key;

                    // add existing total download count and new download count when applicable
                    var totalDownloadCount = existingPackageIdAndVersionStatistic == null ? packageIdAndVersionGroup.Count() : (existingPackageIdAndVersionStatistic.TotalDownloadCount + packageIdAndVersionGroup.Count());
                    packageIdAndVersionStatistic.TotalDownloadCount = totalDownloadCount;

                    statisticsToInsertOrReplace.Add(packageIdAndVersionStatistic);
                }
            }

            // The insert-or-replace-batch methods below will do safe and optimal batching based on partition key and operation count
            await targetTable.InsertOrReplaceBatchAsync(statisticsToInsertOrReplace);
        }
    }
}