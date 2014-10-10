using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public abstract class BatchCollector : Collector
    {
        int _batchSize;

        public BatchCollector(int batchSize)
        {
            _batchSize = batchSize;
        }

        public int BatchCount
        {
            private set;
            get;
        }

        public event Action<CollectorCursor> ProcessedCommit;

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor startFrom)
        {
            CollectorCursor minDependencyCursor = DependentCollections == null ? DateTime.MaxValue : (await GetDependencyCursors(client)).Min();

            CollectorCursor cursor = startFrom;

            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            IEnumerable<JToken> rootItems = root["items"].OrderBy(item => item["commitTimeStamp"].ToObject<DateTime>());

            bool hasPassedDependencies = false;

            foreach (JObject rootItem in rootItems)
            {
                if (hasPassedDependencies)
                {
                    break;
                }

                CollectorCursor pageCursor = (CollectorCursor)rootItem["commitTimeStamp"].ToObject<DateTime>();

                if (pageCursor > startFrom)
                {
                    Uri pageUri = rootItem["@id"].ToObject<Uri>();
                    JObject page = await client.GetJObjectAsync(pageUri);

                    IEnumerable<JToken> pageItems = page["items"].OrderBy(item => item["commitTimeStamp"].ToObject<DateTime>());

                    foreach (JObject pageItem in pageItems)
                    {
                        CollectorCursor itemCursor = (CollectorCursor)pageItem["commitTimeStamp"].ToObject<DateTime>();

                        if (itemCursor > minDependencyCursor)
                        {
                            minDependencyCursor = (await GetDependencyCursors(client)).Min();
                            if (itemCursor > minDependencyCursor)
                            {
                                hasPassedDependencies = true;
                                break;
                            }
                        }

                        if (itemCursor > startFrom)
                        {
                            if (itemCursor > cursor)
                            {
                                // Item timestamp is higher than the previous cursor, so report the previous commit as "processed"
                                OnProcessedCommit(cursor);
                            }
                            // Update the cursor
                            cursor = itemCursor;

                            items.Add(pageItem);

                            if (items.Count == _batchSize)
                            {
                                await ProcessBatch(client, items, (JObject)context);
                                BatchCount++;
                                items.Clear();
                            }
                        }
                    }
                }
            }

            if (items.Count > 0)
            {
                await ProcessBatch(client, items, (JObject)context);
                BatchCount++;
            }

            return cursor;
        }

        private async Task<IEnumerable<CollectorCursor>> GetDependencyCursors(CollectorHttpClient client)
        {
            Task<CollectorCursor>[] cursorValueTasks = DependentCollections.Select(async (cursorUri) =>
            {
                string cursorString = await client.GetStringAsync(cursorUri + "meta/cursor.json");
                JObject cursorObj = JObject.Parse(cursorString);
                return new CollectorCursor(cursorObj["http://schema.nuget.org/collectors/resolver#cursor"]["@value"].ToObject<DateTime>());
            }).ToArray();

            await Task.WhenAll(cursorValueTasks);

            return cursorValueTasks.Select(t => t.Result);
        }

        protected virtual void OnProcessedCommit(CollectorCursor cursor)
        {
            var handler = ProcessedCommit;
            if (handler != null)
            {
                handler(cursor);
            }
        }

        protected abstract Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context);
    }
}
