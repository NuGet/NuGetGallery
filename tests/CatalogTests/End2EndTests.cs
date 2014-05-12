using Catalog;
using Catalog.Maintenance;
using Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    class End2EndTests
    {
        public static async Task Test0Async()
        {
            string path = @"c:\data\site\test";

            Storage storage = new FileStorage
            {
                Path = path,
                Container = "test",
                BaseAddress = "http://localhost:8000/"
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

            ItemCollector collector = new ItemCollector();

            Console.WriteLine("----------------");

            await collector.Run(new Uri("http://localhost:8000/test/catalog/index.json"), new DateTime(2012, 10, 31, 12, 0, 0));

            Console.WriteLine("----------------");

            await collector.Run(new Uri("http://localhost:8000/test/catalog/index.json"), new DateTime(2011, 10, 31, 12, 0, 0));

            Console.WriteLine("----------------");

            await collector.Run(new Uri("http://localhost:8000/test/catalog/index.json"), DateTime.MinValue);
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

        class ItemCollector : Collector
        {
            protected override Emitter CreateEmitter()
            {
                return new ItemEmitter();
            }

            class ItemEmitter : Emitter
            {
                public override async Task<bool> Emit(JObject obj)
                {
                    JToken type;
                    if (obj.TryGetValue("@type", out type) && type.ToString() == "http://test.org/schema#TestItem")
                    {
                        await Task.Factory.StartNew(() => { Console.WriteLine(obj["name"]); });
                        return true;
                    }
                    return false;
                }

                public override async Task Close()
                {
                    await Task.Factory.StartNew(() => {});
                }
            }
        }
    }
}
