using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public abstract class SortingGraphCollector : SortingCollector
    {
        Uri[] _types;

        protected SortingGraphCollector(int batchSize, Uri[] types)
            : base(batchSize)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<string, IList<JObject>> sortedBatch, JObject context)
        {
            IDictionary<string, Task<IGraph>> tasks = new Dictionary<string, Task<IGraph>>();

            foreach (JObject item in sortedBatch.Value)
            {
                if (Utils.IsType(context, item, _types))
                {
                    string itemUri = item["@id"].ToString();
                    tasks.Add(itemUri, client.GetGraphAsync(new Uri(itemUri)));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.Values.ToArray());

                IDictionary<string, IGraph> graphs = new Dictionary<string, IGraph>();

                foreach (KeyValuePair<string, Task<IGraph>> task in tasks)
                {
                    graphs.Add(task.Key, task.Value.Result);
                }

                await ProcessGraphs(new KeyValuePair<string, IDictionary<string, IGraph>>(sortedBatch.Key, graphs));
            }
        }

        protected abstract Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs);
    }
}
