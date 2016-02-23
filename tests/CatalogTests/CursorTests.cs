// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CatalogTests
{
    class CursorTests
    {
        private static DateTime GetDefaultValue(IDictionary<string, string> arguments)
        {
            string defaultValue;
            DateTime defaultValueDT;
            if (!arguments.TryGetValue("-defaultValue", out defaultValue))
            {
                throw new ArgumentException("defaultValue is not provided");
            }

            if (!DateTime.TryParse(defaultValue, out defaultValueDT))
            {
                throw new ArgumentException("defaultValue is not right");
            }
            defaultValueDT = defaultValueDT.ToUniversalTime();

            return defaultValueDT;
        }
        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
            Console.WriteLine(args.Length);

            IDictionary<string, string> result = new Dictionary<string, string>();

            if (args.Length == start)
            {
                return result;
            }

            if ((args.Length - start) % 2 != 0)
            {
                Trace.TraceError("Unexpected number of arguments");
                return null;
            }

            for (int i = start; i < args.Length; i += 2)
            {
                result.Add(args[i], args[i + 1]);
            }

            return result;
        }
        static void TraceRequiredArgument(string name)
        {
            Trace.TraceError("Required argument \"{0}\" not provided", name);
        }
        public static StorageFactory CreateStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            string storageBaseAddress;
            if (!arguments.TryGetValue("-storageBaseAddress", out storageBaseAddress))
            {
                TraceRequiredArgument("-storageBaseAddress");
                return null;
            }

            string storageType;
            if (!arguments.TryGetValue("-storageType", out storageType))
            {
                TraceRequiredArgument("-storageType");
                return null;
            }

            if (storageType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
            {
                string storagePath;
                if (!arguments.TryGetValue("-storagePath", out storagePath))
                {
                    TraceRequiredArgument("-storagePath");
                    return null;
                }

                return new FileStorageFactory(new Uri(storageBaseAddress), storagePath) { Verbose = verbose };
            }
            else if (storageType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
            {
                string storageAccountName;
                if (!arguments.TryGetValue("-storageAccountName", out storageAccountName))
                {
                    TraceRequiredArgument("-storageAccountName");
                    return null;
                }

                string storageKeyValue;
                if (!arguments.TryGetValue("-storageKeyValue", out storageKeyValue))
                {
                    TraceRequiredArgument("-storageKeyValue");
                    return null;
                }

                string storageContainer;
                if (!arguments.TryGetValue("-storageContainer", out storageContainer))
                {
                    TraceRequiredArgument("-storageContainer");
                    return null;
                }

                string storagePath;
                if (!arguments.TryGetValue("-storagePath", out storagePath))
                {
                    TraceRequiredArgument("-storagePath");
                    return null;
                }

                StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureStorageFactory(account, storageContainer, storagePath, new Uri(storageBaseAddress)) { Verbose = verbose };
            }
            else
            {
                Trace.TraceError("Unrecognized storageType \"{0}\"", storageType);
                return null;
            }
        }

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

            await collector.Run(front, back, CancellationToken.None);
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

            DurableCursor front = new DurableCursor(new Uri("http://localhost:8000/cursor/front.json"), storage, MemoryCursor.MinValue);
            //DurableCursor back = new DurableCursor(new Uri("http://localhost:8000/cursor/back.json"), storage);
            MemoryCursor back = MemoryCursor.CreateMax();

            bool didWork = await collector.Run(front, back, CancellationToken.None);

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

            MemoryCursor initial = MemoryCursor.CreateMax();

            string baseAddress = "http://localhost:8000/cursor";
            string path = @"c:\data\site\cursor";
            Storage storage = new FileStorage(baseAddress, path);

            DurableCursor cursorA = new DurableCursor(new Uri("http://localhost:8000/cursor/cursorA.json"), storage, MemoryCursor.MinValue);
            DurableCursor cursorB = new DurableCursor(new Uri("http://localhost:8000/cursor/cursorB.json"), storage, MemoryCursor.MinValue);

            Console.WriteLine("check catalog...");

            bool run = false;

            do
            {
                run = false;
                run |= await collectorA.Run(cursorA, MemoryCursor.CreateMax(), CancellationToken.None);
                run |= await collectorB.Run(cursorB, cursorA, CancellationToken.None);
            }
            while (run);

            Console.WriteLine("ADDING MORE CATALOG");

            await MoreTestCatalog();

            do
            {
                run = false;
                run |= await collectorA.Run(cursorA, MemoryCursor.CreateMax(), CancellationToken.None);
                run |= await collectorB.Run(cursorB, cursorA, CancellationToken.None);
            }
            while (run);

            Console.WriteLine("ALL DONE");
        }

        public static void Test2()
        {
            Test2Async().Wait();
        }

        public static void CreateNewCursor(string[] args)
        {
            IDictionary<string, string> arguments = GetArguments(args, 0);
            StorageFactory storageFactory = CreateStorageFactory(arguments, verbose: true);
            Storage storage = storageFactory.Create();

            DurableCursor cursor = new DurableCursor(storage.ResolveUri("cursor.json"), storage, GetDefaultValue(arguments));
            cursor.Load(CancellationToken.None).Wait();
            cursor.Save(CancellationToken.None).Wait();
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
            await writer.Commit(new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(2));
            await writer.Commit(new DateTime(2014, 1, 3, 0, 0, 0, DateTimeKind.Utc),null, CancellationToken.None);

            writer.Add(new TestCatalogItem(3));
            await writer.Commit(new DateTime(2014, 1, 4, 0, 0, 0, DateTimeKind.Utc),null, CancellationToken.None);

            writer.Add(new TestCatalogItem(4));
            await writer.Commit(new DateTime(2014, 1, 5, 0, 0, 0, DateTimeKind.Utc),null, CancellationToken.None); 

            writer.Add(new TestCatalogItem(5));
            await writer.Commit(new DateTime(2014, 1, 7, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(6));
            await writer.Commit(new DateTime(2014, 1, 8, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None); ;

            writer.Add(new TestCatalogItem(7));
            await writer.Commit(new DateTime(2014, 1, 10, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            Console.WriteLine("test catalog created");
        }

        static async Task MoreTestCatalog()
        {
            string baseAddress = "http://localhost:8000/cursor";
            string path = @"c:\data\site\cursor";

            Storage storage = new FileStorage(baseAddress, path);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 550);

            writer.Add(new TestCatalogItem(8));
            await writer.Commit(new DateTime(2014, 1, 11, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(9));
            await writer.Commit(new DateTime(2014, 1, 13, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(10));
            await writer.Commit(new DateTime(2014, 1, 14, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(11));
            await writer.Commit(new DateTime(2014, 1, 15, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(12));
            await writer.Commit(new DateTime(2014, 1, 17, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(13));
            await writer.Commit(new DateTime(2014, 1, 18, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

            writer.Add(new TestCatalogItem(14));
            await writer.Commit(new DateTime(2014, 1, 20, 0, 0, 0, DateTimeKind.Utc), null, CancellationToken.None);

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
