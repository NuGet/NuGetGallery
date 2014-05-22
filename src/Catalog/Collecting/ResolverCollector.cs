using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class ResolverCollector : BatchCollector
    {
        Storage _storage;
        JObject _resolverFrame;

        public ResolverCollector(Storage storage, int batchSize)
            : base(batchSize)
        {
            Options.InternUris = false;

            _resolverFrame = JObject.Parse(Utils.GetResource("context.Resolver.json"));
            _resolverFrame["@type"] = "Resolver";
            _storage = storage;
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (JObject item in items)
            {
                if (Utils.IsType(context, item, Constants.Package))
                {
                    Uri itemUri = item["url"].ToObject<Uri>();
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
                SparqlResultSet distinctIds = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDistinctPackage.rq"));

                IDictionary<Uri, IGraph> resolverResources = new Dictionary<Uri, IGraph>();

                foreach (SparqlResult row in distinctIds)
                {
                    string id = row["id"].ToString();

                    SparqlParameterizedString sparql = new SparqlParameterizedString();
                    sparql.CommandText = Utils.GetResource("sparql.ConstructResolverGraph.rq");

                    string baseAddress = _storage.BaseAddress + _storage.Container + "/resolver/";

                    sparql.SetLiteral("id", id);
                    sparql.SetLiteral("base", baseAddress);
                    sparql.SetLiteral("extension", ".json");

                    IGraph packageRegistration = SparqlHelpers.Construct(store, sparql.ToString());

                    Uri registrationUri = new Uri(baseAddress + id.ToLowerInvariant() + ".json");
                    resolverResources.Add(registrationUri, packageRegistration);
                }

                if (resolverResources.Count != distinctIds.Count)
                {
                    throw new Exception("resolverResources.Count != distinctIds.Count");
                }

                await MergeAll(resolverResources);
            }
            finally
            {
                store.Dispose();
            }
        }

        async Task MergeAll(IDictionary<Uri, IGraph> resolverResources)
        {
            List<Task> tasks = new List<Task>();
            foreach (KeyValuePair<Uri, IGraph> resolverResource in resolverResources)
            {
                tasks.Add(Task.Run(async () => { await Merge(resolverResource); }));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        async Task Merge(KeyValuePair<Uri, IGraph> resource)
        {
            string existingJson = await _storage.Load(resource.Key);
            if (existingJson != null)
            {
                IGraph existingGraph = Utils.CreateGraph(existingJson);
                resource.Value.Merge(existingGraph);
            }

            string content = Utils.CreateJson(resource.Value, _resolverFrame);
            await _storage.Save("application/json", resource.Key, content);
        }
    }
}
