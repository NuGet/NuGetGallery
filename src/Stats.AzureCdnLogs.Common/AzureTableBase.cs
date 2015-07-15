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
            : this(cloudStorageAccount, typeof(T).Name)
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

        public async Task InsertOrReplaceBatchAsync(IEnumerable<TableEntity> entities)
        {
            var batchOperations = TableOperationBuilder.GetOptimalInsertBatchOperations(entities);
            var count = batchOperations.Count();
            const int maxParallelism = 4;

            for (int i = 0; i < count; i += maxParallelism)
            {
                var tasks = new List<Task>();
                var parallelOperations = batchOperations.Skip(i * maxParallelism).Take(Math.Min(maxParallelism, count - i * maxParallelism));
                foreach (var parallelOperation in parallelOperations)
                {
                    tasks.Add(Task.Run(async () => await _table.ExecuteBatchAsync(parallelOperation)));
                }

                await Task.WhenAll(tasks);
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

        public IReadOnlyCollection<T> GetAllByPartitionKey(string partitionKey)
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException("partitionKey");
            }

            return GetAll(t => t.PartitionKey == partitionKey);
        }

        internal virtual IReadOnlyCollection<T> GetAll(Expression<Func<T, bool>> predicate)
        {
            return GetAll(predicate, null);
        }


        private IReadOnlyCollection<T> GetAll(Expression<Func<T, bool>> predicate, int? limit)
        {
            var query = (IQueryable<T>)_table.CreateQuery<T>();

            if (predicate == null)
            {
                if (limit.HasValue)
                {
                    return query.Take(limit.Value).AsTableQuery().ToList();
                }
                return query.AsTableQuery().ToList();
            }

            if (limit.HasValue)
            {
                return query.Where(predicate).Take(limit.Value).AsTableQuery().ToList();
            }

            return query.Where(predicate).AsTableQuery().ToList();
        }

        protected T Get(Expression<Func<T, bool>> predicate)
        {
            return Query.Where(predicate).FirstOrDefault();
        }
    }
}