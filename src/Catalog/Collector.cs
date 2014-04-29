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
    public class Collector
    {
        static int Calls = 0;

        static int Count = 0;

        static async Task FetchAsync(Uri requestUri, DateTime last, ConcurrentBag<JObject> results, ActionBlock<Uri> actionBlock)
        {
            Interlocked.Increment(ref Calls);

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(requestUri);
            string json = await response.Content.ReadAsStringAsync();
            response.Dispose();
            client.Dispose();

            JObject obj = JObject.Parse(json);

            JToken type;
            if (obj.TryGetValue("@type", out type) && type.ToString() == "Package")
            {
                results.Add(obj);
            }
            else
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
                            Interlocked.Increment(ref Count);
                            actionBlock.Post(next);
                        }
                    }
                }
            }

            int result = Interlocked.Decrement(ref Count);
            if (result == 0)
            {
                actionBlock.Complete();
            }
        }

        public void Run(string baseAddress, DateTime since)
        {
            Uri requestUri = new Uri(baseAddress + "catalog/index.json");

            ConcurrentBag<JObject> results = new ConcurrentBag<JObject>();

            ActionBlock<Uri> baseBlock = null;

            baseBlock = new ActionBlock<Uri>(async (url) =>
            {
                await FetchAsync(url, since, results, baseBlock);
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });

            Count = 0;
            Calls = 0;

            DateTime before = DateTime.Now;

            Interlocked.Increment(ref Count);
            baseBlock.Post(requestUri);
            baseBlock.Completion.Wait();

            DateTime after = DateTime.Now;

            Console.WriteLine("duration {0} seconds", (after - before).TotalSeconds);
            Console.WriteLine("{0} packages since {1} (used {2} http calls)", results.Count, since, Calls);

            Dump(results);
        }

        static void Dump(IEnumerable<JObject> packages)
        {
            foreach (JObject package in packages)
            {
                Console.WriteLine("{0}/{1}", package["id"], package["version"]);
            }
        }
    }
}
