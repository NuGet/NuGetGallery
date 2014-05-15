using Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog.Collecting
{
    public class ResolverDeleteCollector : BatchCollector
    {
        Storage _storage;
        JObject _resolverFrame;

        public ResolverDeleteCollector(Storage storage, int batchSize)
            : base(batchSize)
        {
            Options.InternUris = false;

            _resolverFrame = JObject.Parse(Utils.GetResource("context.ResolverFrame.json"));
            _storage = storage;
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items)
        {
            List<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (JObject item in items)
            {
                Uri itemUri = item["url"].ToObject<Uri>();
                string type = item["@type"].ToString(); 
                if (type == "DeletePackage" || type == "DeletePackageVersion")
                {
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

        async Task ProcessStore(TripleStore store)
        {
            try
            {
                await Task.Run(() => {});
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
