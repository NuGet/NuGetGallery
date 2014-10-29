using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CatalogTests
{
    class CursorTests
    {
        static async Task Test0Async()
        {
            await MakeTestCatalog();

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            TestCollector collector = new TestCollector("Test0", new Uri("http://localhost:8000/cursor/index.json"), handlerFunc);

            DateTime front = new DateTime(2014, 1, 2);
            DateTime back = new DateTime(2014, 1, 6);

            await collector.Run(front, back);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }

        static async Task Test1Async()
        {
            await MakeTestCatalog();

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            TestCollector collector = new TestCollector("Test1", new Uri("http://localhost:8000/cursor/index.json"), handlerFunc);

            string baseAddress = "http://localhost:8000/cursor";
            string path = @"c:\data\site\cursor";
            Storage storage = new FileStorage(baseAddress, path);

            DurableCursor front = new DurableCursor(new Uri("http://localhost:8000/cursor/front.json"), storage, MemoryCursor.Min.Value);
            //DurableCursor back = new DurableCursor(new Uri("http://localhost:8000/cursor/back.json"), storage);
            MemoryCursor back = MemoryCursor.Max;

            bool didWork = await collector.Run(front, back);

            if (!didWork)
            {
                Console.WriteLine("executed but no work was done");
            }
        }

        public static void Test1()
        {
            Test1Async().Wait();
        }

        static async Task Test2Async()
        {
            await MakeTestCatalog();

            Func<HttpMessageHandler> handlerFunc = () =>
            {
                return new FileSystemEmulatorHandler
                {
                    BaseAddress = new Uri("http://localhost:8000"),
                    RootFolder = @"c:\data\site",
                    InnerHandler = new HttpClientHandler()
                };
            };

            TestCollector collectorA = new TestCollector("A", new Uri("http://localhost:8000/cursor/index.json"), handlerFunc);
            TestCollector collectorB = new TestCollector("B", new Uri("http://localhost:8000/cursor/index.json"), handlerFunc);

            MemoryCursor initial = MemoryCursor.Max;

            string baseAddress = "http://localhost:8000/cursor";
            string path = @"c:\data\site\cursor";
            Storage storage = new FileStorage(baseAddress, path);

            DurableCursor cursorA = new DurableCursor(new Uri("http://localhost:8000/cursor/cursorA.json"), storage, MemoryCursor.Min.Value);
            DurableCursor cursorB = new DurableCursor(new Uri("http://localhost:8000/cursor/cursorB.json"), storage, MemoryCursor.Min.Value);

            Console.WriteLine("check catalog...");

            bool run = false;

            do
            {
                run = false;
                run |= await collectorA.Run(cursorA, MemoryCursor.Max);
                run |= await collectorB.Run(cursorB, cursorA);
            }
            while (run);

            Console.WriteLine("ADDING MORE CATALOG");

            await MoreTestCatalog();

            do
            {
                run = false;
                run |= await collectorA.Run(cursorA, MemoryCursor.Max);
                run |= await collectorB.Run(cursorB, cursorA);
            }
            while (run);

            Console.WriteLine("ALL DONE");
        }

        public static void Test2()
        {
            Test2Async().Wait();
        }

        static async Task MakeTestCatalog()
        {
            string baseAddress = "http://localhost:8000/cursor";
            string path = @"c:\data\site\cursor";

            DirectoryInfo folder = new DirectoryInfo(path);
            if (folder.Exists)
            {
                Console.WriteLine("test catalog already created");
                return;
            }

            Storage storage = new FileStorage(baseAddress, path);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            writer.Add(new TestCatalogItem(1));
            await writer.Commit(new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(2));
            await writer.Commit(new DateTime(2014, 1, 3, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(3));
            await writer.Commit(new DateTime(2014, 1, 4, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(4));
            await writer.Commit(new DateTime(2014, 1, 5, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(5));
            await writer.Commit(new DateTime(2014, 1, 7, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(6));
            await writer.Commit(new DateTime(2014, 1, 8, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(7));
            await writer.Commit(new DateTime(2014, 1, 10, 0, 0, 0, DateTimeKind.Utc));

            Console.WriteLine("test catalog created");
        }

        static async Task MoreTestCatalog()
        {
            string baseAddress = "http://localhost:8000/cursor";
            string path = @"c:\data\site\cursor";

            Storage storage = new FileStorage(baseAddress, path);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            writer.Add(new TestCatalogItem(8));
            await writer.Commit(new DateTime(2014, 1, 11, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(9));
            await writer.Commit(new DateTime(2014, 1, 13, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(10));
            await writer.Commit(new DateTime(2014, 1, 14, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(11));
            await writer.Commit(new DateTime(2014, 1, 15, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(12));
            await writer.Commit(new DateTime(2014, 1, 17, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(13));
            await writer.Commit(new DateTime(2014, 1, 18, 0, 0, 0, DateTimeKind.Utc));

            writer.Add(new TestCatalogItem(14));
            await writer.Commit(new DateTime(2014, 1, 20, 0, 0, 0, DateTimeKind.Utc));

            Console.WriteLine("test catalog created");
        }

        class TestCatalogItem : AppendOnlyCatalogItem
        {
            string _id;
            static Uri _type = new Uri("http://tempuri.org/schema#TestItem");

            public TestCatalogItem(int i)
            {
                _id = string.Format("{0}", i);
            }

            public override Uri GetItemType()
            {
                return _type;
            }

            protected override string GetItemIdentity()
            {
                return _id;
            }

            public override StorageContent CreateContent(CatalogContext context)
            {
                return new StringStorageContent(string.Format("item {0}", _id));
            }
        }
    }
}
