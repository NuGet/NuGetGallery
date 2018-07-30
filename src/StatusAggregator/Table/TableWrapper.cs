// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator.Table
{
    public class TableWrapper : ITableWrapper
    {
        public TableWrapper(
            CloudStorageAccount storageAccount, 
            string tableName)
        {
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference(tableName);
        }

        private readonly CloudTable _table;

        public Task CreateIfNotExistsAsync()
        {
            return _table.CreateIfNotExistsAsync();
        }

        public async Task<T> Retrieve<T>(string partitionKey, string rowKey) 
            where T : class, ITableEntity
        {
            var operation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            return (await _table.ExecuteAsync(operation)).Result as T;
        }

        public Task InsertOrReplaceAsync(ITableEntity tableEntity)
        {
            var operation = TableOperation.InsertOrReplace(tableEntity);
            return _table.ExecuteAsync(operation);
        }

        public IQueryable<T> CreateQuery<T>() where T : ITableEntity, new()
        {
            return _table
                .CreateQuery<T>()
                .AsQueryable();
        }
    }
}
