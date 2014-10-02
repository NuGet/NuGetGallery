using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public abstract class Collector
    {
        public IList<Uri> DependentCollections { get; set; }

        static Collector()
        {
            ServicePointManager.DefaultConnectionLimit = 4;
            ServicePointManager.MaxServicePointIdleTime = 10000;
        }

        public Collector()
        {
            DependentCollections = null;
        }

        public async Task<CollectorCursor> Run(Uri index, CollectorCursor last, HttpMessageHandler handler = null)
        {
            CollectorCursor cursor;
            using (CollectorHttpClient client = handler == null ? new CollectorHttpClient() : new CollectorHttpClient(handler))
            {
                cursor = await Fetch(client, index, last);
                RequestCount = client.RequestCount;
            }

            return cursor;
        }

        public async Task<CollectorCursor> Run(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            CollectorCursor cursor = await Fetch(client, index, last);
            RequestCount = client.RequestCount;
            return cursor;
        }

        public int RequestCount
        {
            get;
            private set;
        }

        protected abstract Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last);

        public static async Task<int> GetItemCount(Storage storage)
        {
            Uri resourceUri = storage.ResolveUri("index.json");

            string json = await storage.LoadString(resourceUri);

            if (json == null)
            {
                return 0;
            }

            JObject index = JObject.Parse(json);

            int total = 0;
            foreach (JObject item in index["items"])
            {
                total += item["count"].ToObject<int>();
            }

            return total;
        }
    }
}
