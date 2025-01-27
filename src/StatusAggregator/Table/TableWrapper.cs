// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace StatusAggregator.Table
{
    public class TableWrapper : ITableWrapper
    {
        /// <summary>
        /// The <see cref="ITableEntity.ETag"/> to provide when the existing content in the table is unimportant.
        /// E.g. "if match any".
        /// </summary>
        public const string ETagWildcard = "*";

        public TableWrapper(
            TableServiceClient tableServiceClient, 
            string tableName)
        {

            _table = tableServiceClient?.GetTableClient(tableName) ?? throw new ArgumentNullException(nameof(tableServiceClient));
        }

        private readonly TableClient _table;

        public Task CreateIfNotExistsAsync()
        {
            return _table.CreateIfNotExistsAsync();
        }

        public async Task<T> RetrieveAsync<T>(string rowKey) 
            where T : class, ITableEntity
        {
            return (await _table.GetEntityAsync<T>(TablePartitionKeys.Get<T>(), rowKey))?.Value as T;
        }

        public Task InsertAsync(ITableEntity tableEntity)
        {
            return _table.AddEntityAsync(tableEntity);
        }

        public Task InsertOrReplaceAsync(ITableEntity tableEntity)
        {
            return _table.UpsertEntityAsync(tableEntity, TableUpdateMode.Replace);
        }

        public Task ReplaceAsync(ITableEntity tableEntity)
        {
            return _table.UpsertEntityAsync(tableEntity, TableUpdateMode.Replace);
        }

        public Task DeleteAsync(string partitionKey, string rowKey)
        {
            return _table.DeleteEntityAsync(partitionKey, rowKey);
        }

        public Task DeleteAsync(string partitionKey, string rowKey, string eTag)
        {
            return _table.DeleteEntityAsync(partitionKey, rowKey, new ETag(eTag));
        }

        public Task DeleteAsync(ITableEntity tableEntity)
        {
            return _table.DeleteEntityAsync(tableEntity.PartitionKey, tableEntity.RowKey, tableEntity.ETag);
        }

        public IQueryable<T> CreateQuery<T>() where T : class, ITableEntity, new()
        {
            return _table
                .Query<T>()
                .AsQueryable()
                .Where(e => e.PartitionKey == TablePartitionKeys.Get<T>());
        }
    }
}
