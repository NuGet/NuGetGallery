using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Catalog
{
    public abstract class BatchCollector
    {
        int _batchSize;

        public BatchCollector(int batchSize)
        {
            _batchSize = batchSize;
        }

        public async Task Run(Uri index, DateTime last)
        {
            ServicePointManager.DefaultConnectionLimit = 4;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            WebRequestHandler handler = new WebRequestHandler();
            handler.AllowPipelining = true;

            using (HttpClient client = new HttpClient(handler))
            {
                await FetchBatch(client, index, last);
            }           
        }

        async Task FetchBatch(HttpClient client, Uri index, DateTime last)
        {
            IList<JObject> items = new List<JObject>();

            JObject root = await GetContainer(client, index);

            foreach (JObject rootItem in root["item"])
            {
                DateTime pageTimeStamp = rootItem["published"]["@value"].ToObject<DateTime>();

                if (pageTimeStamp > last)
                {
                    Uri pageUri = new Uri(rootItem["url"].ToString());
                    JObject page = await GetContainer(client, pageUri);

                    foreach (JObject pageItem in page["item"])
                    {
                        DateTime itemTimeStamp = pageItem["published"]["@value"].ToObject<DateTime>();

                        if (itemTimeStamp > last)
                        {
                            Uri itemUri = new Uri(pageItem["url"].ToString());

                            items.Add(pageItem);

                            if (items.Count == _batchSize)
                            {
                                await ProcessBatch(client, items);
                                items.Clear();
                            }
                        }
                    }                    
                }
            }

            if (items.Count > 0)
            {
                await ProcessBatch(client, items);
            }
        }

        protected abstract Task ProcessBatch(HttpClient client, IList<JObject> items);

        static async Task<JObject> GetContainer(HttpClient client, Uri address)
        {
            Console.WriteLine(address);

            string content;
            content = await client.GetStringAsync(address);
            JObject obj = JObject.Parse(content);
            return obj;
        }
    }
}
