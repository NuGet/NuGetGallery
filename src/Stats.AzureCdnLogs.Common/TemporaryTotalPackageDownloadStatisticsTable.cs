// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.AzureCdnLogs.Common
{
    public class TemporaryTotalPackageDownloadStatisticsTable
        : AzureTableBase<TemporaryTotalPackageDownloadStatistic>
    {
        public TemporaryTotalPackageDownloadStatisticsTable(CloudStorageAccount cloudStorageAccount)
            : base(cloudStorageAccount, typeof(PackageStatistics).Name + "totaldownloadstemp")

        {
        }

        public async Task<IReadOnlyCollection<TemporaryTotalPackageDownloadStatistic>> GetNextBatchAsync()
        {
            var items = new List<TemporaryTotalPackageDownloadStatistic>();
            TableContinuationToken token = null;
            var query = new TableQuery<TemporaryTotalPackageDownloadStatistic>().Take(1000);
            const int maxSegments = 5;
            int iteration = 0;

            do
            {
                var segment = await Table.ExecuteQuerySegmentedAsync(query, token);
                items.AddRange(segment.Results);

                token = segment.ContinuationToken;
                iteration++;
            }
            while (iteration < maxSegments && token != null);

            return items;
        }
    }
}