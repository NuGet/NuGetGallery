using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    class End2EndTests
    {
        public static async Task Test0Async()
        {
            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\data\site\test",
            //    Container = "test",
            //    BaseAddress = "http://localhost:8000/"
            //};

            Storage storage = new AzureStorage
            {
                AccountName = "",
                AccountKey = "",
                Container = "test",
                BaseAddress = "http://nuget3.blob.core.windows.net"
            };

            CatalogContext context = new CatalogContext();

            CatalogWriter writer = new CatalogWriter(storage, context, 4, false);

            string[] first = { "john", "paul", "ringo", "george" };
            foreach (string item in first)
            {
                writer.Add(new TestCatalogItem(item));
            }
            await writer.Commit(new DateTime(2010, 12, 25, 12, 0, 0));

            string[] second = { "jimmy", "robert" };
            foreach (string item in second)
            {
                writer.Add(new TestCatalogItem(item));
            }
            await writer.Commit(new DateTime(2011, 12, 25, 12, 0, 0));

            string[] third = { "john-paul", "john" };
            foreach (string item in third)
            {
                writer.Add(new TestCatalogItem(item));
            }
            await writer.Commit(new DateTime(2012, 12, 25, 12, 0, 0));

            //  collection...

            string baseAddress = storage.BaseAddress + storage.Container + "/";

            Uri index = new Uri(baseAddress + "catalog/index.json");

            TestItemCollector collector = new TestItemCollector();

            Console.WriteLine("----------------");

            await collector.Run(index, new DateTime(2012, 10, 31, 12, 0, 0));

            Console.WriteLine("----------------");

            await collector.Run(index, new DateTime(2011, 10, 31, 12, 0, 0));

            Console.WriteLine("----------------");

            await collector.Run(index, DateTime.MinValue);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }
    }
}
