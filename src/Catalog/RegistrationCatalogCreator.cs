using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog
{
    public static class RegistrationCatalogCreator
    {
        public static async Task ProcessGraphs(
            string id,
            IDictionary<string, IGraph> sortedGraphs,
            StorageFactory storageFactory,
            Uri contentBaseAddress,
            int partitionSize,
            int packageCountThreshold)
        {
            try
            {
                Storage storage = storageFactory.Create(id.ToLowerInvariant());

                Uri resourceUri = storage.ResolveUri("index.json");
                string json = await storage.LoadString(resourceUri);

                int count = Utils.CountItems(json);

                int total = count + sortedGraphs.Count;

                if (total < packageCountThreshold)
                {
                    await SaveSmallRegistration(storage, storageFactory.BaseAddress, sortedGraphs, contentBaseAddress, partitionSize);
                }
                else
                {
                    await SaveLargeRegistration(storage, storageFactory.BaseAddress, sortedGraphs, json, contentBaseAddress, partitionSize);
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Process id = {0}", id), e);
            }
        }

        static async Task SaveSmallRegistration(Storage storage, Uri registrationBaseAddress, IDictionary<string, IGraph> items, Uri contentBaseAddress, int partitionSize)
        {
            SingleGraphPersistence graphPersistence = new SingleGraphPersistence(storage);

            await graphPersistence.Initialize();

            await SaveRegistration(storage, registrationBaseAddress, items, null, graphPersistence, contentBaseAddress, partitionSize);

            // now the commit has happened the graphPersistence.Graph should contain all the data

            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", graphPersistence.TypeUri);
            StorageContent content = new StringStorageContent(Utils.CreateJson(graphPersistence.Graph, frame), "application/json", "no-store");
            await storage.Save(graphPersistence.ResourceUri, content);
        }

        static async Task SaveLargeRegistration(Storage storage, Uri registrationBaseAddress, IDictionary<string, IGraph> items, string existingRoot, Uri contentBaseAddress, int partitionSize)
        {
            if (existingRoot != null)
            {
                JToken compacted = JToken.Parse(existingRoot);
                AddExistingItems(Utils.CreateGraph(compacted), items);
            }

            IList<Uri> cleanUpList = new List<Uri>();

            await SaveRegistration(storage, registrationBaseAddress, items, cleanUpList, null, contentBaseAddress, partitionSize);

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

        static async Task SaveRegistration(Storage storage, Uri registrationBaseAddress, IDictionary<string, IGraph> items, IList<Uri> cleanUpList, SingleGraphPersistence graphPersistence, Uri contentBaseAddress, int partitionSize)
        {
            using (RegistrationCatalogWriter writer = new RegistrationCatalogWriter(storage, partitionSize, cleanUpList, graphPersistence))
            {
                foreach (KeyValuePair<string, IGraph> item in items)
                {
                    writer.Add(new RegistrationCatalogItem(new Uri(item.Key), item.Value, contentBaseAddress, registrationBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow);
            }
        }

        static void AddExistingItems(IGraph graph, IDictionary<string, IGraph> items)
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
