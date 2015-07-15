// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
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

        public IReadOnlyCollection<PackageStatistics> GetNextAggregationBatch(PackageStatisticsQueueMessage message)
        {
            // find all records matching the provided partition and row keys
            var records = new List<PackageStatistics>();
            var filterConditions = ConstructFilterConditions(message);

            foreach (var filterCondition in filterConditions)
            {
                var query = new TableQuery<PackageStatistics>().Where(filterCondition);
                var statistics = Table.ExecuteQuery(query);
                records.AddRange(statistics);
            }

            return records;
        }

        private static IReadOnlyCollection<string> ConstructFilterConditions(PackageStatisticsQueueMessage message)
        {
            var filters = new List<string>();

            // max 15 discrete comparisons allowed in $filter
            var partitions = message.PartitionAndRowKeys.Partition(15);

            foreach (IEnumerable<KeyValuePair<string, string>> partition in partitions)
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
                        combinedFilterCondition = TableQuery.CombineFilters(combinedFilterCondition, TableOperators.Or, filterCondition);
                    }
                }

                filters.Add(combinedFilterCondition);
            }

            return filters;
        }
    }
}