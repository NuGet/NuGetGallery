using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Catalog.Collecting
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

        protected override async Task Fetch(CollectorHttpClient client, Uri index, DateTime last)
        {
            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            foreach (JObject rootItem in root["item"])
            {
                DateTime pageTimeStamp = rootItem["timeStamp"]["@value"].ToObject<DateTime>();

                if (pageTimeStamp > last)
                {
                    Uri pageUri = rootItem["url"].ToObject<Uri>();
                    JObject page = await client.GetJObjectAsync(pageUri);

                    foreach (JObject pageItem in page["item"])
                    {
                        DateTime itemTimeStamp = pageItem["timeStamp"]["@value"].ToObject<DateTime>();

                        if (itemTimeStamp > last)
                        {
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
        }

        protected abstract Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context);
    }
}
