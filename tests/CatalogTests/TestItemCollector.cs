using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CatalogTests
{
    public class TestItemCollector : BatchCollector
    {
        public TestItemCollector()
            : base(10)
        {
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<JObject>> tasks = new List<Task<JObject>>();

            foreach (JObject item in items)
            {
                Uri itemUri = new Uri(item["@id"].ToString());
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
