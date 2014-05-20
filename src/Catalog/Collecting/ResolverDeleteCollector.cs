using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class ResolverDeleteCollector : BatchCollector
    {
        Storage _storage;
        JObject _resolverFrame;

        public ResolverDeleteCollector(Storage storage, int batchSize = 200)
            : base(batchSize)
        {
            Options.InternUris = false;

            _resolverFrame = JObject.Parse(Utils.GetResource("context.PackageRegistration.json"));
            _resolverFrame["@type"] = "PackageRegistration";
            _storage = storage;
        }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (JObject item in items)
            {
                if (Utils.IsType(context, item, Constants.DeletePackage) || Utils.IsType(context, item, Constants.DeleteRegistration))
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
                string baseAddress = _storage.BaseAddress + _storage.Container + "/resolver/";

                SparqlResultSet registrationDeletes = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDeleteRegistration.rq"));
                foreach (SparqlResult row in registrationDeletes)
                {
                    string id = row["id"].ToString();

                    Uri resourceUri = new Uri(baseAddress + id + ".json");

                    await _storage.Delete(resourceUri);
                }

                SparqlResultSet packageDeletes = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectDeletePackage.rq"));
                foreach (SparqlResult row in packageDeletes)
                {
                    string id = row["id"].ToString();
                    string version = row["version"].ToString();

                    Uri resourceUri = new Uri(baseAddress + id + ".json");

                    await DeletePackage(resourceUri, version);
                }
            }
            finally
            {
                store.Dispose();
            }
        }

        async Task DeletePackage(Uri resourceUri, string version)
        {
            string existingJson = await _storage.Load(resourceUri);
            if (existingJson != null)
            {
                IGraph currentPackageRegistration = Utils.CreateGraph(existingJson);

                using (TripleStore store = new TripleStore())
                {
                    store.Add(currentPackageRegistration, true);

                    SparqlParameterizedString sparql = new SparqlParameterizedString();
                    sparql.CommandText = Utils.GetResource("sparql.DeletePackage.rq");

                    sparql.SetLiteral("version", version);

                    IGraph modifiedPackageRegistration = SparqlHelpers.Construct(store, sparql.ToString());

                    if (CountPackage(modifiedPackageRegistration) == 0)
                    {
                        await _storage.Delete(resourceUri);
                    }
                    else
                    {
                        string content = Utils.CreateJson(modifiedPackageRegistration, _resolverFrame);
                        await _storage.Save("application/json", resourceUri, content);
                    }
                }
            }
        }

        int CountPackage(IGraph packageRegistration)
        {
            using (TripleStore store = new TripleStore())
            {
                store.Add(packageRegistration, true);

                SparqlResultSet countResultSet = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectPackageCount.rq"));

                foreach (SparqlResult row in countResultSet)
                {
                    string s = ((ILiteralNode)row["count"]).Value;
                    return int.Parse(s);
                }
            }
            return 0;
        }
    }
}
