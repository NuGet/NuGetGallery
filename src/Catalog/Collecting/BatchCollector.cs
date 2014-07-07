using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            CollectorCursor cursor = last;
            DateTime lastDateTime = (DateTime)last;

            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            IEnumerable<JToken> rootItems = root["item"].OrderBy(item => item["timeStamp"].ToObject<DateTime>());

            foreach (JObject rootItem in rootItems)
            {
                DateTime pageTimeStamp = rootItem["timeStamp"].ToObject<DateTime>();

                if (pageTimeStamp > lastDateTime)
                {
                    Uri pageUri = rootItem["url"].ToObject<Uri>();
                    JObject page = await client.GetJObjectAsync(pageUri);

                    IEnumerable<JToken> pageItems = page["item"].OrderBy(item => item["timeStamp"].ToObject<DateTime>());

                    foreach (JObject pageItem in pageItems)
                    {
                        DateTime itemTimeStamp = pageItem["timeStamp"].ToObject<DateTime>();

                        if (itemTimeStamp > lastDateTime)
                        {
                            cursor = itemTimeStamp;

                            Uri itemUri = pageItem["url"].ToObject<Uri>();

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

        protected abstract Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context);
    }
}
