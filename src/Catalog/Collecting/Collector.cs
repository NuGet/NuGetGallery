using Newtonsoft.Json.Linq;
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
        static Collector()
        {
            ServicePointManager.DefaultConnectionLimit = 4;
            ServicePointManager.MaxServicePointIdleTime = 10000;
        }

        public async Task<CollectorCursor> Run(Uri index, CollectorCursor last)
        {
            CollectorCursor cursor;
            using (CollectorHttpClient client = new CollectorHttpClient())
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
    }
}
