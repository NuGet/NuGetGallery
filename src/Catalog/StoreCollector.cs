using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class StoreCollector : BatchCollector
    {
        Uri[] _types;

        public StoreCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, handlerFunc, batchSize)
        {
            Options.InternUris = false;
            _types = types;
        }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (JObject item in items)
            {
                if (Utils.IsType(context, item, _types))
                {
                    Uri itemUri = item["@id"].ToObject<Uri>();
                    tasks.Add(client.GetGraphAsync(itemUri));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());

                TripleStore store = new TripleStore();

                foreach (Task<IGraph> task in tasks)
                {
                    store.Add(task.Result, true);
                }

                await ProcessStore(store);
            }

            return true;
        }

        protected abstract Task ProcessStore(TripleStore store);
    }
}
