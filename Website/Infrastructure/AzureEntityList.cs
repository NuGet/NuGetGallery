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
        private static readonly string INDEX_PARTITION_KEY = "INDEX";
        private static readonly string INDEX_ROW_KEY = "0";

        string _connStr;
        string _tableName;
        CloudTableClient _tableClient;
        CloudTable _tableRef;

        public AzureEntityList(string connStr, string tableName)
        {
            _connStr = connStr;
            _tableName = tableName;
            _tableClient = CloudStorageAccount.Parse(_connStr).CreateCloudTableClient();
            _tableRef = _tableClient.GetTableReference(_tableName);

            // Create the actual Azure Table, if it doesn't yet exist.
            bool newTable = _tableRef.CreateIfNotExists();

            // Create the Index if it doesn't yet exist.
            bool needsIndex = newTable;
            if (!newTable)
            {
                var indexResult = _tableRef.Execute(
                    TableOperation.Retrieve<Index>(INDEX_PARTITION_KEY, INDEX_ROW_KEY));

                needsIndex = (indexResult.HttpStatusCode == 404);
            }

            if (needsIndex)
            {
                // Create the index
                var result = _tableRef.Execute(
                    TableOperation.Insert(new Index
                    {
                        Count = 0,
                        PartitionKey = INDEX_PARTITION_KEY,
                        RowKey = INDEX_ROW_KEY,
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

                // Retrieve ETAG to allow a conditional update
                while (true)
                {
                    var retrievalResult = _tableRef.Execute(
                        TableOperation.Retrieve(value.PartitionKey, value.RowKey));
                    ThrowIfErrorStatus(retrievalResult);

                    value.ETag = retrievalResult.Etag;
                    var storeResult = _tableRef.Execute(TableOperation.Replace(value));
                    if (storeResult.HttpStatusCode == 412)
                    {
                        continue; // retry - ETAG was invalidated.
                    }
                    else
                    {
                        ThrowIfErrorStatus(storeResult);
                        break;
                    }
                }
            }
        }

        public long Add(T entity)
        {
            // 1) Conditional set the entry at the current count (condition: if it doesn't already exist)
            // 2) Increment the list count
            long pos;
            do
            {
                pos = LongCount; // retrieve fresh count each retry
                long page = pos / 1000;
                long row = pos % 1000;
                entity.PartitionKey = FormatPartitionKey(page);
                entity.RowKey = FormatRowKey(row);
            }
            while (!InsertIfNotExists(entity));

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
            for (long page = 0; true; page++)
            {
                string partitionKey = FormatPartitionKey(page);
                var chunkQuery = new TableQuery<T>().Where(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

                var chunk = _tableRef.ExecuteQuery<T>(chunkQuery).ToArray();

                foreach (var item in chunk)
                {
                    yield return item;
                }

                if (chunk.Length < 1000)
                {
                    break;
                }
            }

            yield break;
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

                var chunk = _tableRef.ExecuteQuery<T>(chunkQuery).ToArray();
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

            yield break;
        }

        /// <summary>
        /// FEEL THE LOVE.
        /// </summary>
        private bool InsertIfNotExists<T2>(T2 entity) where T2 : ITableEntity
        {
            // 1) Create a dummy entry to ensure an ETAG exists for the given table partition+row key
            // - the dummy MERGES with existing data instead of overwriting it, so no data loss.
            // 2) Use its ETAG to conditionally replace the item
            // 3) return true if success, false to allow retry on failure
            var dummyResult = _tableRef.Execute(
                TableOperation.InsertOrMerge(new HazardEntry
                {
                    PlaceHeld = 1,
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey,
                }));

            if (!IsSuccess(dummyResult.HttpStatusCode))
            {
                throw new HttpException(dummyResult.HttpStatusCode, "wrong status code");
            }

            entity.ETag = dummyResult.Etag;
            var storeResult = _tableRef.Execute(
                TableOperation.Replace(entity));

            if (storeResult.HttpStatusCode == 412)
            {
                return false; // This entry already exists!
            }

            ThrowIfErrorStatus(storeResult);
            return true; // We created it!
        }

        private long AtomicIncrementCount()
        {
            // 1) retrieve count
            // 2) use ETAG to do a conditional +1 update
            // 3) retry if that optimistic concurrency attempt failed
            while (true)
            {
                var result1 = _tableRef.Execute(
                    TableOperation.Retrieve<Index>(INDEX_PARTITION_KEY, INDEX_ROW_KEY));

                ThrowIfErrorStatus(result1);

                var pos = ((Index)result1.Result).Count;

                // Try to batch update Count and Insert the new item. Either both succeed or both fails.
                var result2 = _tableRef.Execute(
                    TableOperation.Replace(new Index
                    {
                        ETag = result1.Etag,
                        Count = pos + 1,
                        PartitionKey = INDEX_PARTITION_KEY,
                        RowKey = INDEX_ROW_KEY,
                    }));

                if (result2.HttpStatusCode == 412)
                {
                    continue;
                }
                else
                {
                    ThrowIfErrorStatus(result2);
                    return pos; // value before successful increment
                }
            }
        }

        private Index ReadIndex()
        {
            var response = _tableRef.Execute(TableOperation.Retrieve<Index>(INDEX_PARTITION_KEY, INDEX_ROW_KEY));
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
            public int PlaceHeld { get; set; }

            public string ETag { get; set; }
            public string PartitionKey { get; set; }
            public string RowKey{ get; set; }
            public DateTimeOffset Timestamp { get; set; }

            void ITableEntity.ReadEntity(IDictionary<string, EntityProperty> properties, Microsoft.WindowsAzure.Storage.OperationContext operationContext)
            {
                this.PlaceHeld = properties["Place_Held!"].Int32Value;
            }

            IDictionary<string, EntityProperty> ITableEntity.WriteEntity(OperationContext operationContext)
            {
                return new Dictionary<string, EntityProperty>
                {
                    { "Place_Held", EntityProperty.GeneratePropertyForInt(this.PlaceHeld) }
                };
            }
        }

        class Index : ITableEntity
        {
            public long Count { get; set; }

            public string ETag { get; set; }

            public string PartitionKey { get; set; }

            public string RowKey { get; set; }

            public DateTimeOffset Timestamp { get; set; }

            void ITableEntity.ReadEntity(IDictionary<string, EntityProperty> properties, Microsoft.WindowsAzure.Storage.OperationContext operationContext)
            {
                this.Count = properties["Count"].Int64Value;
            }

            IDictionary<string, EntityProperty> ITableEntity.WriteEntity(OperationContext operationContext)
            {
                return new Dictionary<string, EntityProperty>
                {
                    { "Count", EntityProperty.GeneratePropertyForLong(this.Count) }
                };
            }
        }
    }
}