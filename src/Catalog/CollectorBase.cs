using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CollectorBase
    {
        HttpMessageHandler _handler;

        public CollectorBase(Uri index, HttpMessageHandler handler = null)
        {
            _handler = handler;
            Index = index;
            ServicePointManager.DefaultConnectionLimit = 4;
            ServicePointManager.MaxServicePointIdleTime = 10000;
        }

        public Uri Index { get; private set; }

        public int RequestCount { get; private set; }

        public async Task<bool> Run()
        {
            return await Run(MemoryCursor.Min, MemoryCursor.Max);
        }

        public async Task<bool> Run(DateTime front, DateTime back)
        {
            return await Run(new MemoryCursor(front), new MemoryCursor(back));
        }

        public async Task<bool> Run(ReadWriteCursor front, ReadCursor back)
        {
            Trace.TraceInformation("Run ( {0} , {1} )", front, back);

            await Task.WhenAll(front.Load(), back.Load());

            bool result = false;

            using (CollectorHttpClient client = new CollectorHttpClient(_handler))
            {
                result = await Fetch(client, front, back);
                RequestCount = client.RequestCount;
            }
            
            return result;
        }

        protected abstract Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back);
    }
}
