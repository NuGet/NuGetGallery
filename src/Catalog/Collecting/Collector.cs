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

        public async Task Run(Uri index, DateTime last)
        {
            using (CollectorHttpClient client = new CollectorHttpClient())
            {
                await Run(client, index, last);
            }
        }

        public async Task Run(CollectorHttpClient client, Uri index, DateTime last)
        {
            await Fetch(client, index, last);
            RequestCount = client.RequestCount;
        }

        public int RequestCount
        {
            get;
            private set;
        }

        protected abstract Task Fetch(CollectorHttpClient client, Uri index, DateTime last);
    }
}
