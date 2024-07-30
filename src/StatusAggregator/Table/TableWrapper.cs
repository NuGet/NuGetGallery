// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

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
            CloudStorageAccount storageAccount, 
            string tableName)
        {

            var tableClient = storageAccount?.CreateCloudTableClient() 
                ?? throw new ArgumentNullException(nameof(storageAccount));
            _table = tableClient.GetTableReference(tableName);
        }

        private readonly CloudTable _table;

        public Task CreateIfNotExistsAsync()
        {
            return _table.CreateIfNotExistsAsync();
        }

        public async Task<T> RetrieveAsync<T>(string rowKey) 
            where T : class, ITableEntity
        {
            var operation = TableOperation.Retrieve<T>(TablePartitionKeys.Get<T>(), rowKey);
            return (await _table.ExecuteAsync(operation)).Result as T;
        }

        public Task InsertAsync(ITableEntity tableEntity)
        {
            return ExecuteOperationAsync(TableOperation.Insert(tableEntity));
        }

        public Task InsertOrReplaceAsync(ITableEntity tableEntity)
        {
            return ExecuteOperationAsync(TableOperation.InsertOrReplace(tableEntity));
        }

        public Task ReplaceAsync(ITableEntity tableEntity)
        {
            return ExecuteOperationAsync(TableOperation.Replace(tableEntity));
        }

        public Task DeleteAsync(string partitionKey, string rowKey)
        {
            return DeleteAsync(partitionKey, rowKey, ETagWildcard);
        }

        public Task DeleteAsync(string partitionKey, string rowKey, string eTag)
        {
            return DeleteAsync(new TableEntity(partitionKey, rowKey) { ETag = eTag });
        }

        public Task DeleteAsync(ITableEntity tableEntity)
        {
            return ExecuteOperationAsync(TableOperation.Delete(tableEntity));
        }

        private Task ExecuteOperationAsync(TableOperation operation)
        {
            return _table.ExecuteAsync(operation);
        }

        public IQueryable<T> CreateQuery<T>() where T : ITableEntity, new()
        {
            return _table
                .CreateQuery<T>()
                .AsQueryable()
                .Where(e => e.PartitionKey == TablePartitionKeys.Get<T>());
        }
    }
}
