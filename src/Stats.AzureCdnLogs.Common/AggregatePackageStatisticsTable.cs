// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.AzureCdnLogs.Common
{
    public class AggregatePackageStatisticsTable
        : AzureTableBase<PackageDownloadStatistic>
    {
        public AggregatePackageStatisticsTable(CloudStorageAccount cloudStorageAccount)
            : base(cloudStorageAccount, typeof (PackageStatistics).Name + "aggregated")
        {
        }

        public IReadOnlyCollection<PackageDownloadStatistic> GetAggregatePackageStatistics(IEnumerable<string> packageIds)
        {
            var records = new List<PackageDownloadStatistic>();
            var filterConditions = ConstructFilterConditions(packageIds);

            foreach (var filterCondition in filterConditions)
            {
                var query = new TableQuery<PackageDownloadStatistic>().Where(filterCondition);
                var statistics = Table.ExecuteQuery(query);
                records.AddRange(statistics);
            }

            return records;

        }

        private static IReadOnlyCollection<string> ConstructFilterConditions(IEnumerable<string> packageIds)
        {
            var filters = new List<string>();

            // max 15 discrete comparisons allowed in $filter
            var partitions = packageIds.Partition(15);

            foreach (IEnumerable<string> partition in partitions)
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
            }

            return filters;
        }
    }
}