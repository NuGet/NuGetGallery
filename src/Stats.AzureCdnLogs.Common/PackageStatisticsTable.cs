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
    public class PackageStatisticsTable
        : AzureTableBase<PackageStatistics>
    {
        public PackageStatisticsTable(CloudStorageAccount cloudStorageAccount)
            : base(cloudStorageAccount)
        {
        }

        public IReadOnlyCollection<PackageStatistics> GetNextAggregationBatch(IReadOnlyCollection<PackageStatisticsQueueMessage> messages)
        {
            // find all records matching the provided partition and row keys
            var records = new List<PackageStatistics>();
            var filterConditions = ConstructFilterConditions(messages);

            foreach (var filterCondition in filterConditions)
            {
                var query = new TableQuery<PackageStatistics>().Where(filterCondition);
                var statistics = Table.ExecuteQuery(query);
                records.AddRange(statistics);
            }

            return records;
        }

        private static IReadOnlyCollection<string> ConstructFilterConditions(IReadOnlyCollection<PackageStatisticsQueueMessage> messages)
        {
            var filters = new List<string>();

            foreach (var message in messages)
            {
                var filterConditions = ConstructFilterConditions(message);
                filters.AddRange(filterConditions);
            }

            return filters;
        }

        private static IReadOnlyCollection<string> ConstructFilterConditions(PackageStatisticsQueueMessage message)
        {
            var filters = new ConcurrentBag<string>();

            // max 15 discrete comparisons allowed in $filter
            var partitions = message.PartitionAndRowKeys.Partition(15);

            Parallel.ForEach(partitions, partition =>
            {
                string combinedFilterCondition = null;
                foreach (var keyValuePair in partition)
                {
                    var filterCondition = TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, keyValuePair.Value),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, keyValuePair.Key));

                    if (string.IsNullOrEmpty(combinedFilterCondition))
                    {
                        combinedFilterCondition = filterCondition;
                    }
                    else
                    {
                        combinedFilterCondition = TableQuery.CombineFilters(combinedFilterCondition, TableOperators.Or,
                            filterCondition);
                    }
                }

                filters.Add(combinedFilterCondition);
            });

            return filters.ToList();
        }
    }
}