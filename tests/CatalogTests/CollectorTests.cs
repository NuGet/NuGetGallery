using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Collecting.Test;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Diagnostics;
using NuGet.Services.Metadata.Catalog;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;

namespace CatalogTests
{
    class VerboseHandler : FileSystemEmulatorHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Console.WriteLine(request.RequestUri);
            return base.SendAsync(request, cancellationToken);
        }
    }

    class CollectorTests
    {
        /*
        public static async Task Test0Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/resolver/", @"c:\data\site\resolver");

            FileSystemEmulatorHandler handler = new VerboseHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            ResolverCollector collector = new ResolverCollector(storage, 200);

            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue, handler);
            //await collector.Run(new Uri("http://partitions.blob.core.windows.net/partition0/index.json"), DateTime.MinValue);
            //await collector.Run(new Uri("http://localhost:8000/partition/partition0/index.json"), DateTime.MinValue, handler);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test0()
        {
            Console.WriteLine("CollectorTests.Test0");

            Test0Async().Wait();
        }
        */
        public static async Task Test1Async()
        {
            CountCollector collector = new CountCollector();
            await collector.Run(new Uri("http://localhost:8000/test2/ravendb.client.debug/index.json"), DateTime.MinValue);
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
            await collector.Run(new Uri("http://nugetprod0.blob.core.windows.net/ng-catalogs/0/index.json"), DateTime.MinValue);

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

        /*
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

            ResolverCollector collector = new ResolverCollector(storage, 200);

            await collector.Run(new Uri("http://localhost:8000/test/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test4()
        {
            Console.WriteLine("CollectorTests.Test4");

            Test4Async().Wait();
        }

        public static async Task Test5Async()
        {
            Storage storage = new FileStorage("http://localhost:8000/resolver/", @"c:\data\site\resolver");

            TimeSpan prev = TimeSpan.MinValue;
            Uri longest = null;

            for (int i = 0; i < 100; i++)
            {
                Uri indexUri = new Uri(string.Format("http://localhost:8000/partition/partition{0}/index.json", i));

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                
                ResolverCollector collector = new ResolverCollector(storage, 200);
                await collector.Run(indexUri, DateTime.MinValue);
                Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
                
                stopwatch.Stop();

                TimeSpan current = stopwatch.Elapsed;

                if (longest == null || current > prev)
                {
                    longest = indexUri;
                    prev = current;
                }

                Console.WriteLine("{0} {1} seconds", indexUri, current.TotalSeconds);
            }

            Console.WriteLine("the winner is {0}", longest);
        }

        public static void Test5()
        {
            Console.WriteLine("CollectorTests.Test6");

            Test5Async().Wait();
        }

        public static async Task Test6Async()
        {
            FileSystemEmulatorHandler handler = new FileSystemEmulatorHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            Storage storage = new FileStorage("http://localhost:8000/resolver/", @"c:\data\site\resolver");

            ResolverCollector collector = new ResolverCollector(storage, 200);

            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue, handler);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test6()
        {
            Console.WriteLine("CollectorTests.Test6");

            Test6Async().Wait();
        }
        */

        public static async Task Test7Async()
        {
            FileSystemEmulatorHandler handler = new FileSystemEmulatorHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            Storage storage = new FileStorage("http://localhost:8000/nuspec/", @"c:\data\site\nuspec");

            BatchCollector collector = new NuspecCollector(storage, 1);

            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue, handler);
            
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test7()
        {
            Console.WriteLine("CollectorTests.Test7");

            Test7Async().Wait();
        }

        public static async Task Test8Async()
        {
            DistinctPackageIdCollector collector = new DistinctPackageIdCollector(200) { DependentCollections = new List<Uri> { new Uri("http://localhost:8000/test1/"), new Uri("http://localhost:8000/test2/") } };
            await collector.Run(new Uri("http://nugetprod0.blob.core.windows.net/ng-catalogs/0/index.json"), DateTime.MinValue);

            foreach (string s in collector.Result)
            {
                Console.WriteLine(s);
            }

            Console.WriteLine();
            Console.WriteLine("count = {0}", collector.Result.Count);
            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test8()
        {
            Console.WriteLine("CollectorTests.Test8");

            Test8Async().Wait();
        }

        public static async Task Test9Async()
        {
            StorageFactory storage = new FileStorageFactory("http://localhost:8000/test4/", @"c:\data\site\test4");

            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(storage, 200);

            FileSystemEmulatorHandler handler = new VerboseHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue, handler);
            //await collector.Run(new Uri("http://partitions.blob.core.windows.net/partition0/index.json"), DateTime.MinValue);
            //await collector.Run(new Uri("http://localhost:8000/partition/partition0/index.json"), DateTime.MinValue, handler);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test9()
        {
            Console.WriteLine("CollectorTests.Test9");

            Test9Async().Wait();
        }
    }
}
