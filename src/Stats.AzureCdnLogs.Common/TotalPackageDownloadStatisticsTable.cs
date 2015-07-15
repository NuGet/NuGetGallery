// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.AzureCdnLogs.Common
{
    public class TotalPackageDownloadStatisticsTable
        : AzureTableBase<TotalPackageDownloadStatistic>
    {
        public TotalPackageDownloadStatisticsTable(CloudStorageAccount cloudStorageAccount)
            : base(cloudStorageAccount, typeof(PackageStatistics).Name + "totaldownloads")
        {
        }

        public IReadOnlyCollection<TotalPackageDownloadStatistic> GetTotalPackageDownloadStatistics(IEnumerable<string> packageIds)
        {
            var records = new ConcurrentBag<IEnumerable<TotalPackageDownloadStatistic>>();
            var filterConditions = ConstructFilterConditions(packageIds);

            Parallel.ForEach(Partitioner.Create(filterConditions), filterCondition =>
            {
                var query = new TableQuery<TotalPackageDownloadStatistic>().Where(filterCondition);
                var statistics = Table.ExecuteQuery(query);
                records.Add(statistics);
            });

            //foreach (var filterCondition in filterConditions)
            //{
            //    var query = new TableQuery<TotalPackageDownloadStatistic>().Where(filterCondition);
            //    var statistics = Table.ExecuteQuery(query);
            //    records.AddRange(statistics);
            //}

            return records.SelectMany(e => e).ToList();

        }

        private static IReadOnlyCollection<string> ConstructFilterConditions(IEnumerable<string> packageIds)
        {
            var filters = new ConcurrentBag<string>();

            // max 15 discrete comparisons allowed in $filter
            var partitions = packageIds.Partition(15);

            Parallel.ForEach(partitions, partition =>
            {
                string combinedFilterCondition = null;
                foreach (string partitionKey in partition)
                {
                    var filterCondition = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey);

                    if (string.IsNullOrEmpty(combinedFilterCondition))
                    {
                        combinedFilterCondition = filterCondition;
                    }
                    else
                    {
                        combinedFilterCondition = TableQuery.CombineFilters(combinedFilterCondition, TableOperators.Or, filterCondition);
                    }
                }

                filters.Add(combinedFilterCondition);
            });

            return filters.ToList();
        }
    }
}