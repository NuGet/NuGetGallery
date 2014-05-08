using Catalog;
using Catalog.Persistence;
using System;
using System.Linq;
using System.Threading;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace CatalogTests
{
    class CollectorTests
    {
        public static void Test0()
        {
            Uri requestUri = new Uri("http://localhost:8000/full/catalog/index.json");

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
    }
}
