using Catalog;
using Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace CatalogTests
{
    class CollectorTests
    {
        public static Task ForEachAsync<T>(IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            return Task.WhenAll(
                from partition in Partitioner.Create(source).GetPartitions(dop)
                select Task.Run(async delegate
                {
                    using (partition)
                        while (partition.MoveNext())
                            await body(partition.Current);
                }));
        }

        //public static Task ForEachAsync<T>(IEnumerable<T> source, int dop, Func<T, Task> body)
        //{
        //    return Task.WhenAll(
        //        from partition in Partitioner.Create(source).GetPartitions(dop)
        //        select Task.Run(async delegate
        //        {
        //            using (partition)
        //                while (partition.MoveNext())
        //                    await body(partition.Current);
        //        }));
        //}

        public static void Test0()
        {
            //ServicePointManager.DefaultConnectionLimit = 50;

            //Uri requestUri = new Uri("http://localhost:8000/full5/catalog/index.json");
            Uri requestUri = new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json");

            DateTime since = DateTime.MinValue;

            //Collector collector = new PackageCollector(new DistinctCountingTripleStorePackageEmitter(800));
            //Collector collector = new PackageCollector(new DistinctCountingPackageEmitter());
            Collector collector = new PackageCollector(new CountingPackageEmitter());
            //Collector collector = new PackageCollector(new PrintingPackageEmitter());

            collector.Run(requestUri, since).Wait();
        }

        public static void Test1()
        {
            Uri requestUri = new Uri("http://localhost:8000/pub/catalog/index.json");

            DateTime since = DateTime.MinValue;

            TripleStore store = new TripleStore();
            Collector collector = new PackageCollector(new TripleStorePackageEmitter(store));

            collector.Run(requestUri, since).Wait();

            Console.WriteLine("collected {0} triples", store.Triples.Count());
        }

        public static void Test2()
        {
            Uri requestUri = new Uri("http://localhost:8000/pub/catalog/index.json");
            
            DateTime since = DateTime.MinValue;

            TripleStore store = new TripleStore();
            Collector collector = new PackageCollector(new TripleStorePackageEmitter(store));

            long before = GC.GetTotalMemory(true);

            collector.Run(requestUri, since).Wait();

            long after = GC.GetTotalMemory(true);

            Console.WriteLine("before = {0:N0} bytes, after = {1:N0} bytes", before, after);

            InMemoryDataset ds = new InMemoryDataset(store);
            ISparqlQueryProcessor processor = new LeviathanQueryProcessor(ds);
            SparqlQueryParser sparqlparser = new SparqlQueryParser();

            SparqlQuery countQuery = sparqlparser.ParseFromString("SELECT COUNT(?resource) AS ?count WHERE { ?resource a <http://nuget.org/schema#Package> . }");
            SparqlResultSet countResults = (SparqlResultSet)processor.ProcessQuery(countQuery);
            foreach (SparqlResult result in countResults)
            {
                Console.WriteLine("found {0} packages", ((ILiteralNode)result["count"]).Value);
            }

            SparqlQuery distinctQuery = sparqlparser.ParseFromString("SELECT COUNT(DISTINCT ?id) AS ?count WHERE { ?resource a <http://nuget.org/schema#Package> . ?resource <http://nuget.org/schema#id> ?s . BIND (LCASE(?s) AS ?id) }");
            SparqlResultSet distinctResults = (SparqlResultSet)processor.ProcessQuery(distinctQuery);
            foreach (SparqlResult result in distinctResults)
            {
                Console.WriteLine("found {0} packages", ((ILiteralNode)result["count"]).Value);
            }
        }

        public static void Test3()
        {
            DateTime since = DateTime.MinValue;

            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\full",
                Container = "full",
                BaseAddress = "http://localhost:8000"
            };

            //string accountName = "nuget3";
            //string accountKey = "";
            //string connectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", accountName, accountKey);
            //Storage storage = new AzureStorage
            //{
            //    ConnectionString = connectionString,
            //    Container = "full",
            //    BaseAddress = "http://nuget3.blob.core.windows.net/pub"
            //};

            //Uri requestUri = new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json");
            Uri requestUri = new Uri("http://localhost:8000/full/catalog/index.json");

            //Collector collector = new PackageCollector(new ResolverPackageEmitter2(storage, maxBatchSize: 128));
            Collector collector = new PackageCollector(new ResolverPackageEmitter(storage, maxBatchSize: 256));

            collector.Run(requestUri, since, maxDegreeOfParallelism: 4).Wait();

            Console.WriteLine("collection duration: {0} seconds, making {1} http calls (up to {2} in parallel)", collector.Duration, collector.HttpCalls, collector.DegreeOfParallelism);
        }

        public static void Test4()
        {
            DateTime since = DateTime.MinValue;

            Uri requestUri = new Uri("http://localhost:8000/full/catalog/index.json");

            //int i=0;
            //Collector collector = new PackageCollector(new ActionPackageEmitter((o) => {
            //    if (++i % 1000 == 0)
            //    {
            //        Console.WriteLine(i);
            //    }
            //}));

            int i = 0;
            Collector collector = new PackageCollector(new ActionPackageEmitter((o) =>
            {
                if (++i % 10000 == 0)
                {
                    Thread.Sleep(3 * 1000);
                    Console.WriteLine(i);
                }
            }));

            collector.Run(requestUri, since, maxDegreeOfParallelism: 4).Wait();

            Console.WriteLine("total: {0}", i);

            Console.WriteLine("collection duration: {0} seconds, making {1} http calls (up to {2} in parallel)", collector.Duration, collector.HttpCalls, collector.DegreeOfParallelism);
        }

        public static async Task Test5Async()
        {
            ServicePointManager.DefaultConnectionLimit = 100;

            Uri rootUri = new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json");

            HttpClient rootClient = new HttpClient();
            HttpResponseMessage rootResponse = await rootClient.GetAsync(rootUri);

            string rootContent = await rootResponse.Content.ReadAsStringAsync();

            JObject root = JObject.Parse(rootContent);

            ConcurrentQueue<string> items = new ConcurrentQueue<string>();

            int count = 0;

            List<Task> tasks = new List<Task>();

            foreach (JObject rootItem in root["item"])
            {
                Uri pageUri = new Uri(rootItem["url"].ToString());

                HttpClient pageClient = new HttpClient();
                Task<string> pageContent = rootClient.GetStringAsync(pageUri);

                tasks.Add(pageContent.ContinueWith((t) =>
                {
                    JObject page = JObject.Parse(t.Result);

                    foreach (JObject pageItem in page["item"])
                    {
                        items.Enqueue(pageItem["url"].ToString());
                    }
                }));

                Console.WriteLine(pageUri);
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine(items.Count);

            List<string> batch = new List<string>();

            foreach (string itemUri in items)
            {
                batch.Add(itemUri);

                if (batch.Count == 400)
                {
                    List<Task> itemTasks = new List<Task>();

                    foreach (string uri in batch)
                    {
                        using (HttpClient itemClient = new HttpClient())
                        {
                            Task<string> itemContent = rootClient.GetStringAsync(uri);

                            itemTasks.Add(itemContent.ContinueWith((t) =>
                            {
                                JObject data = JObject.Parse(t.Result);

                                int progress = Interlocked.Increment(ref count);
                                if (progress % 1000 == 0)
                                {
                                    Console.WriteLine(progress);
                                }
                            }));
                        }
                    }

                    Task.WaitAll(itemTasks.ToArray());

                    batch.Clear();
                }
            }
        }

        public static void Test5()
        {
            Test5Async().Wait();
        }

        public static async Task Test6Async()
        {
            DateTime last = DateTime.MinValue;

            ServicePointManager.DefaultConnectionLimit = 4;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            Uri rootUri = new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json");
            //Uri rootUri = new Uri("http://localhost:8000/pub/catalog/index.json");

            string rootContent;
            using (HttpClient rootClient = new HttpClient())
            {
                rootContent = await rootClient.GetStringAsync(rootUri);
            }

            JObject root = JObject.Parse(rootContent);

            ConcurrentQueue<string> items = new ConcurrentQueue<string>();

            List<Task> tasks = new List<Task>();

            foreach (JObject rootItem in root["item"])
            {
                string val = rootItem["published"]["@value"].ToString();
                DateTime published = DateTime.Parse(val);

                if (published > last)
                {
                    Uri pageUri = new Uri(rootItem["url"].ToString());

                    HttpClient pageClient = new HttpClient();
                    Task<string> pageContent = pageClient.GetStringAsync(pageUri);

                    tasks.Add(pageContent.ContinueWith((t) =>
                    {
                        try
                        {
                            JObject page = JObject.Parse(t.Result);

                            foreach (JObject pageItem in page["item"])
                            {
                                items.Enqueue(pageItem["url"].ToString());
                            }
                        }
                        finally
                        {
                            pageClient.Dispose();
                        }
                    }));
                }
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine(items.Count);

            int count = 0;

            object lockObj = new object();

            List<string> batch = new List<string>();

            WebRequestHandler requestHandler = new WebRequestHandler();
            requestHandler.AllowPipelining = true;
            //requestHandler.MaxRequestContentBufferSize = long.MaxValue;

            foreach (string itemUri in items)
            {
                batch.Add(itemUri);

                if (batch.Count == 200)
                {
                    TripleStore store = new TripleStore();

                    List<Task> itemTasks = new List<Task>();

                    foreach (string uri in batch)
                    {
                        HttpClient itemClient = new HttpClient(requestHandler);
                        HttpRequestMessage request = new HttpRequestMessage()
                        {
                            RequestUri = new Uri(uri),
                            Method = HttpMethod.Get,
                        };

                        Task<HttpResponseMessage> responseTask = itemClient.SendAsync(request);

                        itemTasks.Add(responseTask.ContinueWith((t) =>
                        {
                            HttpResponseMessage response = responseTask.Result;

                            try
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    string content = response.Content.ReadAsStringAsync().Result;

                                    JObject data = JObject.Parse(content);
                                    IGraph graph = Utils.CreateGraph(data);

                                    lock (lockObj)
                                    {
                                        store.Add(graph, true);
                                    }

                                    int progress = Interlocked.Increment(ref count);
                                    if (progress % 1000 == 0)
                                    {
                                        Console.WriteLine(progress);
                                    }
                                }
                                else
                                {
                                    throw new Exception(string.Format("{0} {1}", uri, response.StatusCode));
                                }
                            }
                            finally
                            {
                                response.Dispose();
                            }
                        }));
                    }

                    Task.WaitAll(itemTasks.ToArray());

                    batch.Clear();

                    ProcessStore(store);
                }
            }
        }

        static void ProcessStore(TripleStore store)
        {
            Console.WriteLine("{0:N0} triple store", store.Triples.Count());
        }

        public static void Test6()
        {
            Test6Async().Wait();
        }

        public static async Task Test7Async()
        {
            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\resolver",
                Container = "resolver",
                BaseAddress = "http://localhost:8000/"
            };

            ResolverCollector collector = new ResolverCollector(storage, 200);

            await collector.Run(new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json"), DateTime.MinValue);
        }

        public static void Test7()
        {
            Test7Async().Wait();
        }
    }
}
