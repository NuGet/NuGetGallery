using Catalog;
using Catalog.Persistence;
using System;
using System.Linq;
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
            Uri requestUri = new Uri("http://localhost:8000/pub/catalog/index.json");

            DateTime since = DateTime.MinValue;

            Collector collector = new PackageCollector(new CountingPackageEmitter());
            //Collector collector = new PackageCollector(new PrintingPackageEmitter());

            collector.Run(requestUri, since);
        }

        public static void Test1()
        {
            Uri requestUri = new Uri("http://localhost:8000/pub/catalog/index.json");

            DateTime since = DateTime.MinValue;

            TripleStore store = new TripleStore();
            Collector collector = new PackageCollector(new TripleStorePackageEmitter(store));

            collector.Run(requestUri, since);

            Console.WriteLine("collected {0} triples", store.Triples.Count());
        }

        public static void Test2()
        {
            Uri requestUri = new Uri("http://localhost:8000/pub/catalog/index.json");
            
            DateTime since = DateTime.MinValue;

            TripleStore store = new TripleStore();
            Collector collector = new PackageCollector(new TripleStorePackageEmitter(store));

            long before = GC.GetTotalMemory(true);

            collector.Run(requestUri, since);

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

            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\pub",
            //    Container = "pub",
            //    BaseAddress = "http://http://nuget3.blob.core.windows.net/pub"
            //};

            string accountName = "nuget3";
            string accountKey = "";
            string connectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", accountName, accountKey);
            Storage storage = new AzureStorage
            {
                ConnectionString = connectionString,
                Container = "feed21",
                BaseAddress = "http://nuget3.blob.core.windows.net"
            };

            Uri requestUri = new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json");

            Collector collector = new PackageCollector(new ResolverPackageEmitter(storage, 400));

            collector.Run(requestUri, since, 32);

            Console.WriteLine("collection duration: {0} seconds, making {1} http calls (up to {2} in parallel)", collector.Duration, collector.HttpCalls, collector.DegreeOfParallelism);
        }
    }
}
