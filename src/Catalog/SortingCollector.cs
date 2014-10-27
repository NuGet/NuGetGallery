using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingCollector : BatchCollector
    {
        public SortingCollector(Uri index, HttpMessageHandler handler = null, int batchSize = 200)
            : base(index, handler, batchSize)
        {
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
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

                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());

            return true;
        }
        protected virtual string GetKey(JObject item)
        {
            return item["nuget:id"].ToString();
        }

        protected abstract Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<string, IList<JObject>> sortedBatch, JObject context);
    }
}
