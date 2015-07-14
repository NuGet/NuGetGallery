// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;

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
            foreach (var kvp in message.PartitionAndRowKeys)
            {
                var record = Get(kvp.Value, kvp.Key);
                records.Add(record);
            }

            return records;
        }
    }
}