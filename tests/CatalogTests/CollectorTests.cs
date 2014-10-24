using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Collecting.Test;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CatalogTests
{
    class CollectorTests
    {
        public static async Task Test0Async()
        {
            //  simply totals up the counts available in the pages

            CountCollector collector = new CountCollector();
            await collector.Run(new Uri("http://nugetjohtaylo.blob.core.windows.net/ver38/catalog/index.json"), DateTime.MinValue);
            Console.WriteLine("total: {0}", collector.Total);
            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test0()
        {
            Console.WriteLine("CollectorTests.Test0");

            Test0Async().Wait();
        }

        public static async Task Test1Async()
        {
            //  attempts to make the http call to the actual item

            Collector collector = new CheckLinksCollector();
            await collector.Run(new Uri("http://localhost:8000/full/index.json"), DateTime.MinValue);

            Console.WriteLine("all done");
        }

        public static void Test1()
        {
            Console.WriteLine("CollectorTests.Test1");

            Test1Async().Wait();
        }

        public static async Task Test2Async()
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

        public static void Test2()
        {
            Console.WriteLine("CollectorTests.Test2");

            Test2Async().Wait();
        }

        public static async Task Test3Async()
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

        public static void Test3()
        {
            Console.WriteLine("CollectorTests.Test3");

            Test3Async().Wait();
        }

        public static async Task Test4Async()
        {
            StorageFactory storageFactory = new FileStorageFactory(new Uri("http://localhost:8000/reg/"), @"c:\data\site\reg");

            //StorageCredentials credentials = new StorageCredentials("", "");
            //CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            //StorageFactory storageFactory = new AzureStorageFactory(account, "reg38", "registration");

            storageFactory.Verbose = true;

            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(storageFactory, 20);

            collector.ContentBaseAddress = new Uri("http://az320820.vo.msecnd.net");

            //collector.PackageCountThreshold = 50;

            FileSystemEmulatorHandler handler = new VerboseFileSystemEmulatorHandler
            {
                BaseAddress = new Uri("http://localhost:8000"),
                RootFolder = @"c:\data\site",
                InnerHandler = new HttpClientHandler()
            };

            //CollectorCursor cursor = new CollectorCursor(new DateTime(2014, 10, 01, 03, 27, 35, 360, DateTimeKind.Utc));
            CollectorCursor cursor = new CollectorCursor(DateTime.MinValue);

            //await collector.Run(new Uri("https://nugetjohtaylo.blob.core.windows.net/ver36/catalog/index.json"), cursor, handler);
            await collector.Run(new Uri("https://localhost:8000/ordered/index.json"), cursor, handler);

            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test4()
        {
            Console.WriteLine("CollectorTests.Test4");

            Test4Async().Wait();
        }

        public static async Task Test5Async()
        {
            VerboseHandler handler = new VerboseHandler();

            FindFirstCollector collector = new FindFirstCollector("xact.ui.web.mvc", "0.0.4773");
            //FindFirstCollector collector = new FindFirstCollector("abot", "1.2.1-alpha");
            await collector.Run(new Uri("http://nugetjohtaylo.blob.core.windows.net/ver38/catalog/index.json"), DateTime.MinValue, handler);

            if (collector.PackageDetails != null)
            {
                Console.WriteLine(collector.PackageDetails);
            }
            else
            {
                Console.WriteLine("Not Found");
            }

            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test5()
        {
            Console.WriteLine("CollectorTests.Test5");

            Test5Async().Wait();
        }
    }
}
