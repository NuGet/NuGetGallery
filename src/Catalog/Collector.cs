using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Catalog
{
    public abstract class Collector
    {
        int _httpCalls = 0;
        int _refCount = 0;

        public int HttpCalls
        {
            get { return _httpCalls; }
        }

        public double Duration
        {
            get;
            private set;
        }

        public int DegreeOfParallelism
        {
            get;
            private set;
        }

        async Task FetchAsync(Uri requestUri, DateTime last, Emitter emitter, ActionBlock<Uri> actionBlock)
        {
            Interlocked.Increment(ref _httpCalls);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(requestUri);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(string.Format("http status code {0} on GET {1}", response.StatusCode, requestUri));
            }

            string json = await response.Content.ReadAsStringAsync();
            response.Dispose();
            client.Dispose();

            JObject obj = JObject.Parse(json);
            if (!emitter.Emit(obj))
            {
                JToken items;
                if (obj.TryGetValue("item", out items))
                {
                    foreach (JObject item in ((JArray)items))
                    {
                        string val = item["published"]["@value"].ToString();
                        DateTime published = DateTime.Parse(val);

                        if (published > last)
                        {
                            Uri next = new Uri(item["url"].ToString());
                            Interlocked.Increment(ref _refCount);
                            actionBlock.Post(next);
                        }
                    }
                }
            }

            int result = Interlocked.Decrement(ref _refCount);
            if (result == 0)
            {
                actionBlock.Complete();
            }
        }

        public void Run(Uri requestUri, DateTime since, int maxDegreeOfParallelism = 4)
        {
            Emitter emitter = CreateEmitter();

            try
            {
                ActionBlock<Uri> baseBlock = null;

                baseBlock = new ActionBlock<Uri>(async (url) =>
                {
                    await FetchAsync(url, since, emitter, baseBlock);
                },
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism });

                _refCount = 0;
                _httpCalls = 0;

                DateTime before = DateTime.Now;

                Interlocked.Increment(ref _refCount);
                baseBlock.Post(requestUri);
                baseBlock.Completion.Wait();

                DateTime after = DateTime.Now;

                Duration = (after - before).TotalSeconds;
                DegreeOfParallelism = maxDegreeOfParallelism;
            }
            finally
            {
                emitter.Close();
            }
        }

        protected abstract Emitter CreateEmitter();
    }
}
