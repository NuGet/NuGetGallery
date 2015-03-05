using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationPersistence
    {
        Uri _registrationUri;
        int _packageCountThreshold;
        int _partitionSize;
        RecordingStorage _storage;
        Uri _registrationBaseAddress;

        public RegistrationPersistence(StorageFactory storageFactory, string id, int partitionSize, int packageCountThreshold)
        {
            _storage = new RecordingStorage(storageFactory.Create(id.ToLowerInvariant()));
            _registrationUri = _storage.ResolveUri("index.json");
            _packageCountThreshold = packageCountThreshold;
            _partitionSize = partitionSize;
            _registrationBaseAddress = storageFactory.BaseAddress;
        }

        public Task<IDictionary<RegistrationKey, Tuple<string, IGraph>>> Load()
        {
            return Load(_storage, _registrationUri);
        }

        public async Task Save(IDictionary<RegistrationKey, Tuple<string, IGraph>> resulting)
        {
            foreach (var item in resulting)
            {
                Console.WriteLine("Save: {0} : {1}", item.Key, item.Value.Item1);
            }

            await Save(_storage, _registrationBaseAddress, resulting, _partitionSize, _packageCountThreshold);

            await Cleanup(_storage);
        }

        //  Load implementation

        static async Task<IDictionary<RegistrationKey, Tuple<string, IGraph>>> Load(IStorage storage, Uri resourceUri)
        {
            IGraph graph = await LoadCatalog(storage, resourceUri);

            IDictionary<RegistrationKey, Tuple<string, IGraph>> resources = GetResources(graph);

            return resources;
        }

        static IDictionary<RegistrationKey, Tuple<string, IGraph>> GetResources(IGraph graph)
        {
            IDictionary<RegistrationKey, Tuple<string, IGraph>> resources = new Dictionary<RegistrationKey, Tuple<string, IGraph>>();

            TripleStore store = new TripleStore();
            store.Add(graph);

            IList<Uri> existingItems = ListExistingItems(store);

            foreach (Uri existingItem in existingItems)
            {
                AddExistingItem(resources, store, existingItem);
            }

            return resources;
        }

        static IList<Uri> ListExistingItems(TripleStore store)
        {
            string sparql = Utils.GetResource("sparql.SelectInlinePackage.rq");

            SparqlResultSet resultSet = SparqlHelpers.Select(store, sparql);

            IList<Uri> results = new List<Uri>();
            foreach (SparqlResult result in resultSet)
            {
                IUriNode item = (IUriNode)result["catalogPackage"];
                results.Add(item.Uri);
            }
            return results;
        }

        static void AddExistingItem(IDictionary<RegistrationKey, Tuple<string, IGraph>> resources, TripleStore store, Uri catalogEntry)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.ConstructCatalogEntryGraph.rq");
            sparql.SetUri("catalogEntry", catalogEntry);

            IGraph graph = SparqlHelpers.Construct(store, sparql.ToString());

            INode subject = graph.CreateUriNode(catalogEntry);

            string id = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Id)).First().Object.ToString();
            string version = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Version)).First().Object.ToString();

            RegistrationKey key = new RegistrationKey { Id = id, Version = version };

            resources.Add(key, Tuple.Create(catalogEntry.AbsoluteUri, graph));
        }

        static async Task<IGraph> LoadCatalog(IStorage storage, Uri resourceUri)
        {
            string json = await storage.LoadString(resourceUri);

            IGraph graph = Utils.CreateGraph(json);

            if (graph == null)
            {
                return new Graph();
            }

            IEnumerable<Triple> pages = graph.GetTriplesWithPredicateObject(graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogPage));

            IList<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (Triple page in pages)
            {
                Uri pageUri = ((IUriNode)page.Subject).Uri;
                if (pageUri != resourceUri)
                {
                    tasks.Add(LoadCatalogPage(storage, pageUri));
                }
            }

            await Task.WhenAll(tasks.ToArray());

            foreach (Task<IGraph> task in tasks)
            {
                graph.Merge(task.Result, true);
            }

            return graph;
        }

        static async Task<IGraph> LoadCatalogPage(IStorage storage, Uri pageUri)
        {
            string json = await storage.LoadString(pageUri);
            IGraph graph = Utils.CreateGraph(json);
            return graph;
        }

        //  Save implementation

        static async Task Save(IStorage storage, Uri registrationBaseAddress, IDictionary<RegistrationKey, Tuple<string, IGraph>> resulting, int partitionSize, int packageCountThreshold)
        {
            IDictionary<string, IGraph> items = new Dictionary<string, IGraph>();

            foreach (Tuple<string, IGraph> value in resulting.Values)
            {
                items.Add(value.Item1, value.Item2);
            }

            if (items.Count < packageCountThreshold)
            {
                await SaveSmallRegistration(storage, registrationBaseAddress, items, partitionSize);
            }
            else
            {
                await SaveLargeRegistration(storage, registrationBaseAddress, items, partitionSize);
            }
        }

        static async Task SaveSmallRegistration(IStorage storage, Uri registrationBaseAddress, IDictionary<string, IGraph> items, int partitionSize)
        {
            SingleGraphPersistence graphPersistence = new SingleGraphPersistence(storage);

            await graphPersistence.Initialize();

            await SaveRegistration(storage, registrationBaseAddress, items, null, graphPersistence, partitionSize);

            // now the commit has happened the graphPersistence.Graph should contain all the data

            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", graphPersistence.TypeUri);
            StorageContent content = new StringStorageContent(Utils.CreateJson(graphPersistence.Graph, frame), "application/json", "no-store");
            await storage.Save(graphPersistence.ResourceUri, content);
        }

        static async Task SaveLargeRegistration(IStorage storage, Uri registrationBaseAddress, IDictionary<string, IGraph> items, int partitionSize)
        {
            IList<Uri> cleanUpList = new List<Uri>();

            await SaveRegistration(storage, registrationBaseAddress, items, cleanUpList, null, partitionSize);
        }

        static async Task SaveRegistration(IStorage storage, Uri registrationBaseAddress, IDictionary<string, IGraph> items, IList<Uri> cleanUpList, SingleGraphPersistence graphPersistence, int partitionSize)
        {
            using (RegistrationMakerCatalogWriter writer = new RegistrationMakerCatalogWriter(storage, partitionSize, cleanUpList, graphPersistence))
            {
                foreach (KeyValuePair<string, IGraph> item in items)
                {
                    writer.Add(new RegistrationMakerCatalogItem(new Uri(item.Key), item.Value, registrationBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow);
            }
        }

        static async Task Cleanup(RecordingStorage storage)
        {
            IList<Task> tasks = new List<Task>();
            foreach (Uri loaded in storage.Loaded)
            {
                if (!storage.Saved.Contains(loaded))
                {
                    tasks.Add(storage.Delete(loaded));
                }
            }
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }
    }
}
