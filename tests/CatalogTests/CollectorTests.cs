using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Test;
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

            CountCollector collector = new CountCollector(new Uri("http://nugetjohtaylo.blob.core.windows.net/ver38/catalog/index.json"));
            await collector.Run();
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

            CollectorBase collector = new CheckLinksCollector(new Uri("http://localhost:8000/full/index.json"));
            await collector.Run();

            Console.WriteLine("all done");
        }

        public static void Test1()
        {
            Console.WriteLine("CollectorTests.Test1");

            Test1Async().Wait();
        }

        public static async Task Test2Async()
        {
            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new VerboseHandler();
            };

            DistinctPackageIdCollector collector = new DistinctPackageIdCollector(new Uri("https://az320820.vo.msecnd.net/catalog-0/index.json"), handlerFunc);
            await collector.Run();

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
            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            Storage storage = new FileStorage("http://localhost:8000/nuspec/", @"c:\data\site\nuspec");

            BatchCollector collector = new NuspecCollector(new Uri("http://localhost:8000/full/index.json"), storage, handlerFunc, 1);

            await collector.Run();
            
            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test3()
        {
            Console.WriteLine("CollectorTests.Test3");

            Test3Async().Wait();
        }

        public static async Task Test4Async()
        {
            StorageFactory storageFactory = new FileStorageFactory(new Uri("http://localhost:8000/reg2/"), @"c:\data\site\reg2");

            //StorageCredentials credentials = new StorageCredentials("", "");
            //CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            //StorageFactory storageFactory = new AzureStorageFactory(account, "reg38", "registration");

            storageFactory.Verbose = true;

            Uri index = new Uri("https://localhost:8000/dotnetrdf/index.json");
            //Uri index = new Uri("https://localhost:8000/ordered/index.json");
            //Uri index = new Uri("https://nugetjohtaylo.blob.core.windows.net/ver36/catalog/index.json");

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(index, storageFactory, handlerFunc, 20);

            collector.ContentBaseAddress = new Uri("http://az320820.vo.msecnd.net");

            //collector.PackageCountThreshold = 50;
            //CollectorCursor cursor = new CollectorCursor(new DateTime(2014, 10, 01, 03, 27, 35, 360, DateTimeKind.Utc));

            await collector.Run();

            Console.WriteLine("http requests: {0} batch count: {1}", collector.RequestCount, collector.BatchCount);
        }

        public static void Test4()
        {
            Console.WriteLine("CollectorTests.Test4");

            Test4Async().Wait();
        }

        public static async Task Test5Async()
        {
            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new VerboseHandler();
            };

            Uri index = new Uri("https://az320820.vo.msecnd.net/catalog-0/index.json");

            FindFirstCollector collector = new FindFirstCollector(index, "HtmlTags", "1.2.0.145", handlerFunc);
            //FindFirstCollector collector = new FindFirstCollector("abot", "1.2.1-alpha");

            await collector.Run();

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

        public static async Task Test6Async()
        {
            //VerboseHandler handler = new VerboseHandler();

            //FindFirstCollector collector = new FindFirstCollector("xact.ui.web.mvc", "0.0.4773");
            //FindFirstCollector collector = new FindFirstCollector("abot", "1.2.1-alpha");

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            PrintCollector collector = new PrintCollector("Test6", new Uri("http://localhost:8000/ordered/index.json"), handlerFunc);

            await collector.Run();

            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test6()
        {
            Console.WriteLine("CollectorTests.Test6");

            Test6Async().Wait();
        }

        public static async Task Test7Async()
        {
            //VerboseHandler handler = new VerboseHandler();

            //FindFirstCollector collector = new FindFirstCollector("xact.ui.web.mvc", "0.0.4773");
            //FindFirstCollector collector = new FindFirstCollector("abot", "1.2.1-alpha");

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            CollectorBase collector = new PrintCommitCollector(new Uri("http://localhost:8000/dotnetrdf/index.json"), handlerFunc);

            await collector.Run();

            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static void Test7()
        {
            Console.WriteLine("CollectorTests.Test7");

            Test7Async().Wait();
        }
    }
}
