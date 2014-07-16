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
    public class ResolverCollector : StoreCollector
    {
        Storage _storage;
        JObject _resolverFrame;

        public ICollectorLogger Logger { get; set; }

        public string GalleryBaseAddress { get; set; }
        public string CdnBaseAddress { get; set; }

        public ResolverCollector(Storage storage, int batchSize)
            : base(batchSize, new Uri[] { Schema.DataTypes.Package })
        {
            _resolverFrame = JObject.Parse(Utils.GetResource("context.Resolver.json"));
            _resolverFrame["@type"] = "PackageRegistration";
            _storage = storage;
        }

        protected override async Task ProcessStore(TripleStore store)
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

                    string baseAddress = _storage.BaseAddress.ToString();

                    sparql.SetLiteral("id", id);
                    sparql.SetLiteral("base", baseAddress);
                    sparql.SetLiteral("extension", ".json");
                    sparql.SetLiteral("galleryBase", GalleryBaseAddress);
                    sparql.SetLiteral("cdnBase", CdnBaseAddress);

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
            string existingJson = await _storage.LoadString(resource.Key);
            if (existingJson != null)
            {
                IGraph existingGraph = Utils.CreateGraph(existingJson);
                resource.Value.Merge(existingGraph);
            }

            StorageContent content = new StringStorageContent(
                Utils.CreateJson(resource.Value, _resolverFrame), 
                contentType: "application/json", 
                cacheControl: "public, max-age=300, s-maxage=300");
            await _storage.Save(resource.Key, content);
        }
    }
}
