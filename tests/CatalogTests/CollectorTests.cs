using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Collecting.Test;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CatalogTests
{
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
            //  simply totals up the counts available in the pages

            CountCollector collector = new CountCollector();
            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue);
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
            //  attempts to make the http call to the actual item

            Collector collector = new CheckLinksCollector();
            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue);

            Console.WriteLine("all done");
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
            //StorageFactory storageFactory = new FileStorageFactory(new Uri("http://localhost:8000/reg/"), @"c:\data\site\reg");

            StorageCredentials credentials = new StorageCredentials("", "");
            CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            StorageFactory storageFactory = new AzureStorageFactory(account, "ver3", "registration");

            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(storageFactory, 200);

            //collector.PackageCountThreshold = 50;

            FileSystemEmulatorHandler handler = new VerboseHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            //CollectorCursor cursor = new CollectorCursor(new DateTime(2014, 10, 01, 03, 27, 35, 360, DateTimeKind.Utc));
            CollectorCursor cursor = new CollectorCursor(DateTime.MinValue);

            await collector.Run(new Uri("http://localhost:8000/test/index.json"), cursor, handler);
            //await collector.Run(new Uri("http://localhost:8000/ravendb/index.json"), cursor, handler);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test9()
        {
            Console.WriteLine("CollectorTests.Test9");

            Test9Async().Wait();
        }

        public static async Task Test10Async()
        {
            //Storage storage = new FileStorage(new Uri("http://preview.nuget.org/ver3-preview/packageinfo/"), @"c:\data\site\info");

            StorageCredentials credentials = new StorageCredentials("", "");
            CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            Storage storage = new AzureStorage(account, "ver3");

            PackageInfoCatalogCollector collector = new PackageInfoCatalogCollector(storage, 200);

            collector.RegistrationBaseAddress = new Uri(storage.BaseAddress, "registration");

            //collector.RegistrationBaseAddress = new Uri("http://preview.nuget.org/ver3-preview/registrations/");
            //collector.BaseAddress = new Uri("http://preview.nuget.org/ver3-preview/registrations/");

            FileSystemEmulatorHandler handler = new VerboseHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            //CollectorCursor cursor = new CollectorCursor(new DateTime(2014, 10, 01, 03, 27, 35, 360, DateTimeKind.Utc));
            CollectorCursor cursor = new CollectorCursor(DateTime.MinValue);

            //await collector.Run(new Uri("http://localhost:8000/full/index.json"), cursor, handler);
            await collector.Run(new Uri("http://localhost:8000/test/index.json"), cursor, handler);
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test10()
        {
            Console.WriteLine("CollectorTests.Test9");

            Test10Async().Wait();
        }
    }
}
