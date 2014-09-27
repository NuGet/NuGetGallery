using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            IList<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (JObject item in sortedBatch.Value)
            {
                if (Utils.IsType(context, item, _types))
                {
                    Uri itemUri = item["url"].ToObject<Uri>();
                    tasks.Add(client.GetGraphAsync(itemUri));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());

                IList<IGraph> graphs = new List<IGraph>();

                foreach (Task<IGraph> task in tasks)
                {
                    graphs.Add(task.Result);
                }

                await ProcessGraphs(new KeyValuePair<string, IList<IGraph>>(sortedBatch.Key, graphs));
            }
        }

        protected abstract Task ProcessGraphs(KeyValuePair<string, IList<IGraph>> sortedGraphs);
    }
}
