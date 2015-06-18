// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;

namespace Stats.ParseAzureCdnLogs
{
    internal static class TableOperationBuilder
    {
        public static TableOperation CreateInsertOperation(CdnLogEntry entity)
        {
            // reverse chronological order of log entries
            entity.RowKey = RowKeyBuilder.CreateReverseChronological(entity.EdgeServerTimeDelivered);

            // parition by date
            entity.PartitionKey = entity.EdgeServerTimeDelivered.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            var tableOperation = TableOperation.Insert(entity);
            return tableOperation;
        }

        /// <summary>
        /// Ensures all operations within a <see cref="TableBatchOperation"/> target the same <see cref="ITableEntity.PartitionKey"/>
        /// and do not exceed the maximum of 100 <see cref="TableOperation"/> instances per batch.
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        public static IEnumerable<TableBatchOperation> GetOptimalInsertBatchOperations(IEnumerable<CdnLogEntry> entities)
        {
            const int maxBatchSize = 100;
            var list = new List<TableBatchOperation>();
            var dictionary = new Dictionary<string, TableBatchOperation>();

            foreach (var entity in entities)
            {
                var tableOperation = CreateInsertOperation(entity);

                if (!dictionary.ContainsKey(entity.PartitionKey))
                {
                    // ensure batch inserts happen on the same partition key
                    dictionary.Add(entity.PartitionKey, new TableBatchOperation());
                }

                TableBatchOperation tableBatchOperation = dictionary[entity.PartitionKey];
                tableBatchOperation.Add(tableOperation);
            }

            foreach (var kvp in dictionary)
            {
                TableBatchOperation tableBatchOperation = kvp.Value;
                int pointer = maxBatchSize;
                IEnumerable<TableOperation> tableOperations = tableBatchOperation.Take(pointer);
                while (tableOperations.Any())
                {
                    var newTableBatchOperation = new TableBatchOperation();
                    foreach (var tableOperation in tableOperations)
                    {
                        newTableBatchOperation.Add(tableOperation);
                    }
                    list.Add(newTableBatchOperation);

                    // fetch the next batch of operations
                    tableOperations = tableBatchOperation.Skip(pointer).Take(maxBatchSize);
                    pointer += maxBatchSize;
                }
            }

            return list;
        }
    }
}