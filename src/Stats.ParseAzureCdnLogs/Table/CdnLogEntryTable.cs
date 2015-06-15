// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.ParseAzureCdnLogs
{
    public class CdnLogEntryTable
    {
        private readonly CloudTable _table;

        public CdnLogEntryTable(CloudStorageAccount cloudStorageAccount, string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = typeof(CdnLogEntry).Name.ToLowerInvariant();
            }

            var tableClient = cloudStorageAccount.CreateCloudTableClient();
            tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(500), 3);
            tableClient.DefaultRequestOptions.PayloadFormat = TablePayloadFormat.JsonNoMetadata;
            _table = tableClient.GetTableReference(tableName);
        }

        public async Task<bool> CreateIfNotExists()
        {
            return await _table.CreateIfNotExistsAsync();
        }

        public async Task InsertBatchAsync(IEnumerable<CdnLogEntry> entities)
        {
            var tableBatchOperation = new TableBatchOperation();
            foreach (var entity in entities)
            {
                var tableOperation = CreateInsertOperation(entity);
                tableBatchOperation.Add(tableOperation);
            }

            await _table.ExecuteBatchAsync(tableBatchOperation);
        }

        private static TableOperation CreateInsertOperation(CdnLogEntry entity)
        {
            // reverse chronological order of log entries
            entity.RowKey = RowKeyBuilder.CreateReverseChronological(entity.EdgeServerTimeDelivered);

            // parition by date
            entity.PartitionKey = entity.EdgeServerTimeDelivered.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            var tableOperation = TableOperation.Insert(entity);
            return tableOperation;
        }
    }
}