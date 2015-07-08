// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Stats.AzureCdnLogs.Common;

namespace Stats.ParseAzureCdnLogs
{
    public class PackageStatisticTable
    {
        private readonly CloudTable _table;

        public PackageStatisticTable(CloudStorageAccount cloudStorageAccount)
        {
            var tableName = typeof(PackageStatistics).Name.ToLowerInvariant();
            var tableClient = cloudStorageAccount.CreateCloudTableClient();
            tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(500), 3);
            tableClient.DefaultRequestOptions.PayloadFormat = TablePayloadFormat.JsonNoMetadata;
            _table = tableClient.GetTableReference(tableName);
        }

        public async Task<bool> CreateIfNotExists()
        {
            return await _table.CreateIfNotExistsAsync();
        }

        public async Task InsertBatchAsync(IEnumerable<PackageStatistics> entities)
        {
            foreach (var batchOperation in TableOperationBuilder.GetOptimalInsertBatchOperations(entities))
            {
                await _table.ExecuteBatchAsync(batchOperation);
            }
        }
    }
}