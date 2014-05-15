using Catalog;
using Catalog.Collecting;
using Catalog.Maintenance;
using Catalog.Persistence;
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

            ItemCollector collector = new ItemCollector();

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

        class TestCatalogItem : CatalogItem
        {
            string _name;
            string _type;

            public TestCatalogItem(string name)
            {
                _name = name;
                _type = "http://test.org/schema#TestItem";
            }

            public override string CreateContent(CatalogContext context)
            {
                string id = GetBaseAddress() + _name + ".json";

                JObject obj = new JObject
                {
                    { "name", _name },
                    { "@id", id },
                    { "@type", _type },
                    { "@context", new JObject { { "@vocab", "http://test.org/schema#" } } }
                };

                return obj.ToString();
            }

            public override string GetItemType()
            {
                return _type;
            }

            protected override string GetItemName()
            {
                return _name;
            }
        }

        class ItemCollector : BatchCollector
        {
            public ItemCollector()
                : base(10)
            {
            }

            protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items)
            {
                List<Task<JObject>> tasks = new List<Task<JObject>>();

                foreach (JObject item in items)
                {
                    Uri itemUri = new Uri(item["url"].ToString());
                    string type = item["@type"].ToString();
                    if (type == "TestItem")
                    {
                        tasks.Add(client.GetJObjectAsync(itemUri));
                    }
                }

                await Task.WhenAll(tasks.ToArray());

                foreach (Task<JObject> task in tasks)
                {
                    Console.WriteLine(task.Result["name"]);
                }
            }
        }
    }
}
