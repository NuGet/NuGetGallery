using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Collecting.Test;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace CatalogTests
{
    class CollectorTests
    {

        public static async Task Test0Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/resolver/", @"c:\data\site\resolver");

            ResolverCollector collector = new ResolverCollector(storage, 1) { GalleryBaseAddress = "http://dev.nugettest.org", CdnBaseAddress = "http://az320820.vo.msecnd.net" };

            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue);
            //await collector.Run(new Uri("https://nuget3.blob.core.windows.net/test20140708/index.json"), DateTime.MinValue);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test0()
        {
            Console.WriteLine("CollectorTests.Test0");

            Test0Async().Wait();
        }

        public static async Task Test1Async()
        {
            CountCollector collector = new CountCollector();
            await collector.Run(new Uri("http://localhost:8000/export/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine("total: {0}", collector.Total);
            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test1()
        {
            Console.WriteLine("CollectorTests.Test1");

            Test1Async().Wait();
        }

        public static async Task Test2Async()
        {
            Collector collector = new CheckLinksCollector();
            await collector.Run(new Uri("http://nuget3.blob.core.windows.net/pub/catalog/index.json"), DateTime.MinValue);
        }

        public static void Test2()
        {
            Console.WriteLine("CollectorTests.Test2");

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
            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test3()
        {
            Console.WriteLine("CollectorTests.Test3");

            Test3Async().Wait();
        }

        public static async Task Test4Async()
        {
            //Storage storage = new AzureStorage
            //{
            //    AccountName = "nuget3",
            //    AccountKey = "",
            //    Container = "feed",
            //    BaseAddress = "http://nuget3.blob.core.windows.net"
            //};

            // Storage storage = new FileStorage("http://localhost:8000/", @"c:\data\site\test");
            Storage storage = new AzureStorage(
                CloudStorageAccount.Parse("AccountName=nugetdev0;AccountKey=;DefaultEndpointsProtocol=https"),
                "cdn-public",
                "v3/resolver",
                new Uri("http://preview-api.dev.nugettest.org/v3/resolver/"));

            ResolverCollector collector = new ResolverCollector(storage, 200) { GalleryBaseAddress = "http://dev.nugettest.org", CdnBaseAddress = "http://az320820.vo.msecnd.net" };

            await collector.Run(new Uri("http://localhost:8000/test/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test4()
        {
            Console.WriteLine("CollectorTests.Test4");

            Test4Async().Wait();
        }
    }
}
