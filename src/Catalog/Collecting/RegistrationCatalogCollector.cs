using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogCollector : SortingGraphCollector
    {
        StorageFactory _storageFactory;

        public RegistrationCatalogCollector(StorageFactory storageFactory, int batchSize)
            : base(batchSize, new Uri[] { Schema.DataTypes.Package })
        {
            _storageFactory = storageFactory;

            ContentBaseAddress = new Uri("http://tempuri.org");

            PartitionSize = 64;
            PackageCountThreshold = 128;
        }

        public Uri ContentBaseAddress { get; set; }
        public int PartitionSize { get; set; }
        public int PackageCountThreshold { get; set; }

        protected override async Task ProcessGraphs(KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs)
        {
            try
            {
                Storage storage = _storageFactory.Create(sortedGraphs.Key);

                Uri resourceUri = storage.ResolveUri("index.json");
                string json = await storage.LoadString(resourceUri);

                int count = Utils.CountItems(json);

                int total = count + sortedGraphs.Value.Count;

                if (total < PackageCountThreshold)
                {
                    await SaveSmallRegistration(storage, sortedGraphs.Value);
                }
                else
                {
                    await SaveLargeRegistration(storage, sortedGraphs.Value, json);
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Process id = {0}", sortedGraphs.Key), e);
            }
        }

        async Task SaveSmallRegistration(Storage storage, IDictionary<string, IGraph> items)
        {
            SingleGraphPersistence graphPersistence = new SingleGraphPersistence(storage);

            await graphPersistence.Initialize();

            await SaveRegistration(storage, items, null, graphPersistence);

            // now the commit has happened the graphPersistence.Graph should contain all the data

            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", graphPersistence.TypeUri);
            StorageContent content = new StringStorageContent(Utils.CreateJson(graphPersistence.Graph, frame), "application/json", "no-store");
            await storage.Save(graphPersistence.ResourceUri, content);
        }

        async Task SaveLargeRegistration(Storage storage, IDictionary<string, IGraph> items, string existingRoot)
        {
            if (existingRoot != null)
            {
                JToken compacted = JToken.Parse(existingRoot);
                AddExistingItems(Utils.CreateGraph(compacted), items);
            }

            IList<Uri> cleanUpList = new List<Uri>();

            await SaveRegistration(storage, items, cleanUpList, null);

            // because there were multiple files some might now be irrelevant

            foreach (Uri uri in cleanUpList)
            {
                if (uri != storage.ResolveUri("index.json"))
                {
                    Console.WriteLine("DELETE: {0}", uri);
                    await storage.Delete(uri);
                }
            }
        }

        async Task SaveRegistration(Storage storage, IDictionary<string, IGraph> items, IList<Uri> cleanUpList, SingleGraphPersistence graphPersistence)
        {
            using (RegistrationCatalogWriter writer = new RegistrationCatalogWriter(storage, PartitionSize, cleanUpList, graphPersistence))
            {
                foreach (KeyValuePair<string, IGraph> item in items)
                {
                    writer.Add(new RegistrationCatalogItem(item.Key, item.Value, ContentBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow);
            }
        }

        void AddExistingItems(IGraph graph, IDictionary<string, IGraph> items)
        {
            TripleStore store = new TripleStore();
            store.Add(graph, true);

            string inlinePackageSparql = Utils.GetResource("sparql.SelectInlinePackage.rq");

            SparqlResultSet rows = SparqlHelpers.Select(store, inlinePackageSparql);
            foreach (SparqlResult row in rows)
            {
                string packageUri = ((IUriNode)row["catalogPackage"]).Uri.AbsoluteUri;
                items[packageUri] = graph;
            }
        }
    }
}
