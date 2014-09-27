using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public abstract class SortingCollector : BatchCollector
    {
        protected SortingCollector(int batchSize) : base(batchSize)
        {
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            IDictionary<string, IList<JObject>> sortedItems = new Dictionary<string, IList<JObject>>();

            foreach (JObject item in items)
            {
                string key = GetKey(item);

                IList<JObject> itemList;
                if (!sortedItems.TryGetValue(key, out itemList))
                {
                    itemList = new List<JObject>();
                    sortedItems.Add(key, itemList);
                }

                itemList.Add(item);
            }

            IList<Task> tasks = new List<Task>();

            foreach (KeyValuePair<string, IList<JObject>> sortedBatch in sortedItems)
            {
                Task task = ProcessSortedBatch(client, sortedBatch, context);

                //DEBUG
                await task;

                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());
        }
        protected virtual string GetKey(JObject item)
        {
            return item["nuget:id"].ToString();
        }

        protected abstract Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<string, IList<JObject>> sortedBatch, JObject context);
    }
}
