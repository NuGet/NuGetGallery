using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public abstract class StoreCollector : BatchCollector
    {
        Uri[] _types;

        public StoreCollector(int batchSize, Uri[] types)
            : base(batchSize)
        {
            Options.InternUris = false;
            _types = types;
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
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
        }

        protected abstract Task ProcessStore(TripleStore store);
    }
}
