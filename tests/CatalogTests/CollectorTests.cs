using Catalog.Collecting;
using Catalog.Collecting.Test;
using Catalog.Persistence;
using System;
using System.Threading.Tasks;

namespace CatalogTests
{
    class CollectorTests
    {

        public static async Task Test0Async()
        {
            //Storage storage = new AzureStorage
            //{
            //    AccountName = "nuget3",
            //    AccountKey = "",
            //    Container = "feed",
            //    BaseAddress = "http://nuget3.blob.core.windows.net"
            //};

            Storage storage = new FileStorage
            {
                Path = @"c:\data\site\pub",
                Container = "feed",
                BaseAddress = "http://localhost:8000/"
            };

            ResolverCollector collector = new ResolverCollector(storage, 200);

            await collector.Run(new Uri("http://localhost:8000/pub/catalog/index.json"), DateTime.MinValue);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }

        public static async Task Test1Async()
        {
            CountCollector collector = new CountCollector();
            await collector.Run(new Uri("http://localhost:8000/pub/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine(collector.Total);
        }

        public static void Test1()
        {
            Test1Async().Wait();
        }

        public static async Task Test2Async()
        {
            Collector collector = new CheckLinksCollector();
            await collector.Run(new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json"), DateTime.MinValue);
        }

        public static void Test2()
        {
            Test2Async().Wait();
        }

        public static async Task Test3Async()
        {
            DistinctPackageIdCollector collector = new DistinctPackageIdCollector(200);
            await collector.Run(new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json"), DateTime.MinValue);

            foreach (string s in collector.Result)
            {
                Console.WriteLine(s);
            }

            Console.WriteLine();
            Console.WriteLine("count = {0}", collector.Result.Count);
        }

        public static void Test3()
        {
            Test3Async().Wait();
        }
    }
}
