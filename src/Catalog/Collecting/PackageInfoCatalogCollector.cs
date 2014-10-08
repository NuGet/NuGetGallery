using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class PackageInfoCatalogCollector : BatchCollector
    {
        Storage _storage;
        Uri[] _types;

        public PackageInfoCatalogCollector(Storage storage, int batchSize, CatalogContext context = null)
            : base(batchSize)
        {
            _storage = storage;
            _types = new Uri[] { Schema.DataTypes.Package };

            Context = context ?? new CatalogContext();

            RegistrationBaseAddress = new Uri("http://tempuri.org/registration/");
        }

        public Uri RegistrationBaseAddress { get; set; }

        public Uri BaseAddress { get; set; }

        public CatalogContext Context { get; private set; }

        protected override async Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            IList<Task> tasks = new List<Task>();

            foreach (JObject item in items)
            {
                Task task = ProcessItem(client, item, context);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks.ToArray());
        }

        async Task ProcessItem(CollectorHttpClient client, JObject item, JObject context)
        {
            if (Utils.IsType(context, item, _types))
            {
                Uri itemUri = item["url"].ToObject<Uri>();

                IGraph itemGraph = await client.GetGraphAsync(itemUri);

                await CreatePackageInfo(itemUri, itemGraph);
            }
        }

        async Task CreatePackageInfo(Uri itemUri, IGraph itemGraph)
        {
            IGraph packageInfoGraph = CreatePackageInfoContent(itemUri, itemGraph);

            Uri packageInfoAddress = GetPackageInfoAddress(packageInfoGraph);

            await _storage.Save(packageInfoAddress, CreateIndexContent(packageInfoGraph, Schema.DataTypes.PackageInfo));
        }

        IGraph CreatePackageInfoContent(Uri itemUri, IGraph itemGraph)
        {
            IGraph contentGraph;

            Uri baseAddress = BaseAddress ?? _storage.BaseAddress;

            using (TripleStore store = new TripleStore())
            {
                store.Add(itemGraph, true);

                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = Utils.GetResource("sparql.ConstructPackageInfoContentGraph.rq");

                sparql.SetUri("package", itemUri);
                sparql.SetLiteral("baseAddress", baseAddress.AbsoluteUri.Trim('/') + '/');
                sparql.SetLiteral("registrationBaseAddress", RegistrationBaseAddress.AbsoluteUri.Trim('/') + '/');

                contentGraph = SparqlHelpers.Construct(store, sparql.ToString());
            }

            return contentGraph;
        }

        Uri GetPackageInfoAddress(IGraph packageInfoGraph)
        {
            Triple triple = packageInfoGraph.GetTriplesWithPredicateObject(
                packageInfoGraph.CreateUriNode(Schema.Predicates.Type),
                packageInfoGraph.CreateUriNode(Schema.DataTypes.PackageInfo)).FirstOrDefault();

            return ((IUriNode)triple.Subject).Uri;
        }

        StorageContent CreateIndexContent(IGraph graph, Uri type)
        {
            JObject frame = Context.GetJsonLdContext("context.PackageInfo.json", type);
            return new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }
    }
}
