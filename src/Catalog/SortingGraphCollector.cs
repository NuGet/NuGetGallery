using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingGraphCollector : SortingCollector
    {
        Uri[] _types;

        public SortingGraphCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<string, IList<JObject>> sortedBatch, JToken context)
        {
            ConcurrentDictionary<string, IGraph> graphs = new ConcurrentDictionary<string, IGraph>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;

            Parallel.ForEach(sortedBatch.Value, options, item =>
            {
                if (Utils.IsType((JObject)context, item, _types))
                {
                    string itemUri = item["@id"].ToString();
                    var task = client.GetGraphAsync(new Uri(itemUri));
                    task.Wait();

                    if (!graphs.TryAdd(itemUri, task.Result))
                    {
                        throw new Exception("Duplicate graph: " + itemUri);
                    }
                }
            });

            await ProcessGraphs(new KeyValuePair<string, IDictionary<string, IGraph>>(sortedBatch.Key, graphs));
        }

        protected abstract Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs);
    }
}
