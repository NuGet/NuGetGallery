using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Infrastructure
{
    /// <summary>
    /// Random-access list/stack thing backed by Azure Table Store. 
    /// Supports add-to-end (push) and set (update item) but no other add/remove.
    /// </summary>
    public class AzureEntityList<T> : IEnumerable<T> where T : ITableEntity, new()
    {
        private const string IndexPartitionKey = "INDEX";
        private const string IndexRowKey = "0";

        private CloudTable _tableRef;

        public AzureEntityList(string connStr, string tableName)
        {
            var tableClient = CloudStorageAccount.Parse(connStr).CreateCloudTableClient();
            _tableRef = tableClient.GetTableReference(tableName);

            // Create the actual Azure Table, if it doesn't yet exist.
            bool newTable = _tableRef.CreateIfNotExists();

            // Create the Index if it doesn't yet exist.
            bool needsIndex = newTable;
            if (!newTable)
            {
                var indexResult = _tableRef.Execute(
                    TableOperation.Retrieve<Index>(IndexPartitionKey, IndexRowKey));

                needsIndex = (indexResult.HttpStatusCode == 404);
            }

            if (needsIndex)
            {
                // Create the index
                var result = _tableRef.Execute(
                    TableOperation.Insert(new Index
                    {
                        Count = 0,
                        PartitionKey = IndexPartitionKey,
                        RowKey = IndexRowKey,
                    }));

                ThrowIfErrorStatus(result);
            }
        }

        /// <summary>
        /// FEEL THE LOVE. CAN BECOME STALE AT ANY TIME, OBVIOUSLY.
        /// </summary>
        public int Count
        {
            get
            {
                return (int)(ReadIndex().Count);
            }
        }

        public long LongCount
        {
            get
            {
                return ReadIndex().Count;
            }
        }

        /// <summary>
        /// FEEL THE LOVE.
        /// </summary>
        public T this[long index]
        {
            get
            {
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException("index", index, "Negative indexes are invalid.");
                }

                if (index >= LongCount)
                {
                    throw new ArgumentOutOfRangeException("index", index, "Index does not exist");
                }

                long page = index / 1000;
                long row = index % 1000;
                string partitionKey = FormatPartitionKey(page);
                string rowKey = FormatRowKey(row);

                var response = _tableRef.Execute(TableOperation.Retrieve<T>(partitionKey, rowKey));
                if (response.HttpStatusCode == 404)
                {
                    throw new ArgumentOutOfRangeException("index", index, "(404) Error - Not Found");
                }

                ThrowIfErrorStatus(response);

                return (T)response.Result;
            }
            set
            {
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException("index", index, "Negative indexes are invalid.");
                }

                if (index >= LongCount)
                {
                    throw new ArgumentOutOfRangeException("index", index, "Index does not exist");
                }

                long page = index / 1000;
                long row = index % 1000;
                value.PartitionKey = FormatPartitionKey(page);
                value.RowKey = FormatRowKey(row);

                // Just do an unconditional update - if you wanted any *real* benefit of atomic update then you would need a more complex method signature that calls you back when optimistic updates fail ETAG checks!
                _tableRef.Execute(TableOperation.Replace(value));
            }
        }

        public long Add(T entity)
        {
            // 1) Conditionally insert the entry at the current count (condition: if it doesn't already exist)
            // 2) Increment the list count
            long pos = -1; // To avoid compiler warnings, grr - should never be returned
            InsertIfNotExistsWithRetry(() =>
            {
                pos = LongCount; // retrieve fresh count each retry
                long page = pos / 1000;
                long row = pos % 1000;
                entity.PartitionKey = FormatPartitionKey(page);
                entity.RowKey = FormatRowKey(row);
                return entity;
            });

            // We got an entry in place - now all we need to do is update the count!
            long oldCount = AtomicIncrementCount();
            Debug.Assert(oldCount == pos);
            Debug.Assert(LongCount > oldCount);
            return pos;
        }

        /// <summary>
        /// NOTE - ASSUME THAT THIS WORKS BY PAGING THROUGH YOUR WHOLE TABLE WITH SYNCHRONOUS REQUESTS.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            for (long page = 0;; page++)
            {
                string partitionKey = FormatPartitionKey(page);
                var chunkQuery = new TableQuery<T>().Where(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

                var chunk = _tableRef.ExecuteQuery(chunkQuery).ToArray();

                foreach (var item in chunk)
                {
                    yield return item;
                }

                if (chunk.Length < 1000)
                {
                    break;
                }
            }
        }

        public IEnumerable<T> GetRange(long pos, int n)
        {
            if (pos < 0)
            {
                throw new IndexOutOfRangeException("Negative indexes are invalid");
            }

            int done = 0;
            long page = pos / 1000;
            long offset = pos % 1000;
            while (done < n)
            {
                string partitionKey = FormatPartitionKey(page);
                string rowKey = FormatRowKey(offset);
                var chunkQuery = new TableQuery<T>().Where(
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, rowKey)));

                var chunk = _tableRef.ExecuteQuery(chunkQuery).ToArray();
                if (chunk.Length == 0)
                {
                    break; // Reached the end of the list
                }

                foreach (var item in chunk)
                {
                    if (done < n)
                    {
                        yield return item;
                        done += 1;
                    }
                }

                page += 1;
                offset = 0;
            }
        }

        public static long GetLogicalIndex(T entity)
        {
            // Chop off leading "Page_"
            long page = Int64.Parse(entity.PartitionKey.Substring(5), CultureInfo.InvariantCulture);
            long offset = Int32.Parse(entity.RowKey, CultureInfo.InvariantCulture);
            return (1000 * page) + offset;
        }

        private long AtomicIncrementCount()
        {
            // 1) retrieve count
            // 2) use ETAG to do a conditional +1 update
            // 3) retry if that optimistic concurrency attempt failed
            long pos = -1; // To avoid compiler warnings, grr - should never be returned
            DoReplaceWithRetry(() =>
            {
                var result1 = _tableRef.Execute(
                    TableOperation.Retrieve<Index>(IndexPartitionKey, IndexRowKey));

                ThrowIfErrorStatus(result1);
                pos = ((Index) result1.Result).Count;
                return new Index
                {
                    ETag = result1.Etag,
                    Count = pos + 1,
                    PartitionKey = IndexPartitionKey,
                    RowKey = IndexRowKey,
                };
            });

            return pos; // value before successful increment
        }

        /// <summary>
        /// FEEL THE LOVE.
        /// </summary>
        private void InsertIfNotExistsWithRetry<T2>(Func<T2> valueGenerator) where T2 : ITableEntity
        {
            TableResult storeResult;
            do
            {
                var entity = valueGenerator();

                // 1) Create a dummy entry to ensure an ETAG exists for the given table partition+row key
                // - the dummy MERGES with existing data instead of overwriting it, so no data loss.
                // 2) Use its ETAG to conditionally replace the item
                // 3) return true if success, false to allow retry on failure
                var dummyResult = _tableRef.Execute(
                    TableOperation.InsertOrMerge(new HazardEntry
                    {
                        PartitionKey = entity.PartitionKey,
                        RowKey = entity.RowKey,
                    }));

                if (!IsSuccess(dummyResult.HttpStatusCode))
                {
                    throw new HttpException(dummyResult.HttpStatusCode, "wrong status code");
                }

                entity.ETag = dummyResult.Etag;
                storeResult = _tableRef.Execute(TableOperation.Replace(entity));
            }
            while (storeResult.HttpStatusCode == 412);
            ThrowIfErrorStatus(storeResult);
        }

        private void DoReplaceWithRetry<T2>(Func<T2> valueGenerator) where T2 : ITableEntity
        {
            TableResult storeResult;
            do
            {
                storeResult = _tableRef.Execute(TableOperation.Replace(valueGenerator.Invoke()));
            }
            while (storeResult.HttpStatusCode == 412);
            ThrowIfErrorStatus(storeResult);
        }

        private Index ReadIndex()
        {
            var response = _tableRef.Execute(TableOperation.Retrieve<Index>(IndexPartitionKey, IndexRowKey));
            return (Index)response.Result;
        }

        private static string FormatPartitionKey(long page)
        {
            return "Page_" + page.ToString("D19", CultureInfo.InvariantCulture);
        }

        private static string FormatRowKey(long pageRow)
        {
            Debug.Assert(pageRow < 1000);
            return pageRow.ToString("D3", CultureInfo.InvariantCulture);
        }

        private static bool IsSuccess(int statusCode)
        {
            return statusCode >= 200 && statusCode < 300;
        }

        private static void ThrowIfErrorStatus(TableResult result)
        {
            if (!IsSuccess(result.HttpStatusCode))
            {
                throw new HttpException(result.HttpStatusCode, "Http status code indicates a problem occurred");
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class HazardEntry : ITableEntity
        {
            private const string PlaceHolderPropertyName = "Place_Held";

            public string ETag { get; set; }
            public string PartitionKey { get; set; }
            public string RowKey{ get; set; }
            public DateTimeOffset Timestamp { get; set; }

            void ITableEntity.ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                // We don't really need to implement this. This entity is write-only.
            }

            IDictionary<string, EntityProperty> ITableEntity.WriteEntity(OperationContext operationContext)
            {
                return new Dictionary<string, EntityProperty>
                {
                    { PlaceHolderPropertyName, EntityProperty.GeneratePropertyForInt(1) }
                };
            }
        }

        class Index : ITableEntity
        {
            private const string CountPropertyName = "Count";

            public long Count { get; set; }

            public string ETag { get; set; }

            public string PartitionKey { get; set; }

            public string RowKey { get; set; }

            public DateTimeOffset Timestamp { get; set; }

            void ITableEntity.ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                Count = properties[CountPropertyName].Int64Value ?? 0;
            }

            IDictionary<string, EntityProperty> ITableEntity.WriteEntity(OperationContext operationContext)
            {
                return new Dictionary<string, EntityProperty>
                {
                    { CountPropertyName, EntityProperty.GeneratePropertyForLong(Count) }
                };
            }
        }
    }
}