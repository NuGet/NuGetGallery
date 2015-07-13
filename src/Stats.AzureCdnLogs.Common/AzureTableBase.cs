// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Queryable;

namespace Stats.AzureCdnLogs.Common
{
    public abstract class AzureTableBase<T>
        where T : TableEntity, new()
    {
        private readonly CloudTable _table;

        protected AzureTableBase(CloudStorageAccount cloudStorageAccount)
            :this(cloudStorageAccount, typeof(T).Name)
        {
        }

        protected AzureTableBase(CloudStorageAccount cloudStorageAccount, string tableName)
        {
            tableName = tableName.ToLowerInvariant();
            var tableClient = cloudStorageAccount.CreateCloudTableClient();
            tableClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(500), 3);
            tableClient.DefaultRequestOptions.PayloadFormat = TablePayloadFormat.JsonNoMetadata;
            _table = tableClient.GetTableReference(tableName);
        }

        protected CloudTable Table
        {
            get { return _table; }
        }

        protected IQueryable<T> Query
        {
            get { return _table.CreateQuery<T>().AsTableQuery(); }
        }

        public virtual async Task<bool> CreateIfNotExistsAsync()
        {
            return await _table.CreateIfNotExistsAsync();
        }

        public async Task InsertBatchAsync(IEnumerable<TableEntity> entities)
        {
            foreach (var batchOperation in TableOperationBuilder.GetOptimalInsertBatchOperations(entities))
            {
                await _table.ExecuteBatchAsync(batchOperation);
            }
        }

        public T Get(string partitionKey, string rowKey)
        {
            if (partitionKey == null && rowKey != null)
            {
                return GetByRowKey(rowKey);
            }

            if (partitionKey != null && rowKey == null)
            {
                return GetByPartitionKey(partitionKey);
            }

            // at this point, both params are null, but we can throw specific ArgumentNullExceptions
            if (partitionKey == null)
            {
                throw new ArgumentNullException("partitionKey");
            }

            if (rowKey == null)
            {
                throw new ArgumentNullException("rowKey");
            }

            return Get(t => t.PartitionKey == partitionKey && t.RowKey == rowKey);
        }

        public T GetByPartitionKey(string partitionKey)
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException("partitionKey");
            }

            return Get(t => t.PartitionKey == partitionKey);
        }

        public T GetByRowKey(string rowKey)
        {
            if (rowKey == null)
            {
                throw new ArgumentNullException("rowKey");
            }

            return Get(t => t.RowKey == rowKey);
        }

        public void AddOrUpdate(T element)
        {
            element.ETag = "*";
            _table.Execute(TableOperation.InsertOrReplace(element));
        }

        protected T Get(Expression<Func<T, bool>> predicate)
        {
            return Query.Where(predicate).FirstOrDefault();
        }
    }
}