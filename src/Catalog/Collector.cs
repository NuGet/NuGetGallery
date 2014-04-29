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
        static int HttpCalls = 0;
        static int RefCount = 0;

        async Task FetchAsync(Uri requestUri, DateTime last, Emitter emitter, ActionBlock<Uri> actionBlock)
        {
            Interlocked.Increment(ref HttpCalls);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(requestUri);
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
                            Interlocked.Increment(ref RefCount);
                            actionBlock.Post(next);
                        }
                    }
                }
            }

            int result = Interlocked.Decrement(ref RefCount);
            if (result == 0)
            {
                actionBlock.Complete();
            }
        }

        public void Run(string baseAddress, DateTime since, int maxDegreeOfParallelism = 4)
        {
            Uri requestUri = new Uri(baseAddress + "catalog/index.json");

            Emitter emitter = CreateEmitter();

            try
            {
                ActionBlock<Uri> baseBlock = null;

                baseBlock = new ActionBlock<Uri>(async (url) =>
                {
                    await FetchAsync(url, since, emitter, baseBlock);
                },
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism });

                RefCount = 0;
                HttpCalls = 0;

                DateTime before = DateTime.Now;

                Interlocked.Increment(ref RefCount);
                baseBlock.Post(requestUri);
                baseBlock.Completion.Wait();

                DateTime after = DateTime.Now;

                Console.WriteLine("duration {0} seconds, {1} http calls", (after - before).TotalSeconds, HttpCalls);
            }
            finally
            {
                emitter.Close();
            }
        }

        protected abstract Emitter CreateEmitter();
    }
}
