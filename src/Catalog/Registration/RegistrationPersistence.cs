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
    public class RegistrationPersistence : IRegistrationPersistence
    {
        Uri _registrationUri;
        int _packageCountThreshold;
        int _partitionSize;
        RecordingStorage _storage;
        Uri _registrationBaseAddress;

        public RegistrationPersistence(StorageFactory storageFactory, RegistrationKey registrationKey, int partitionSize, int packageCountThreshold)
        {
            _storage = new RecordingStorage(storageFactory.Create(registrationKey.ToString()));
            _registrationUri = _storage.ResolveUri("index.json");
            _packageCountThreshold = packageCountThreshold;
            _partitionSize = partitionSize;
            _registrationBaseAddress = storageFactory.BaseAddress;
        }

        public Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load()
        {
            return Load(_storage, _registrationUri);
        }

        public async Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration)
        {
            await Save(_storage, _registrationBaseAddress, registration, _partitionSize, _packageCountThreshold);

            await Cleanup(_storage);
        }

        //  Load implementation

        static async Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load(IStorage storage, Uri resourceUri)
        {
            IGraph graph = await LoadCatalog(storage, resourceUri);

            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources = GetResources(graph);

            return resources;
        }

        static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> GetResources(IGraph graph)
        {
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources = new Dictionary<RegistrationEntryKey, RegistrationCatalogEntry>();

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

        static void AddExistingItem(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources, TripleStore store, Uri catalogEntry)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.ConstructCatalogEntryGraph.rq");
            sparql.SetUri("catalogEntry", catalogEntry);

            IGraph graph = SparqlHelpers.Construct(store, sparql.ToString());

            resources.Add(RegistrationCatalogEntry.Promote(catalogEntry.AbsoluteUri, graph));
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

            await LoadCatalogItems(storage, graph);

            return graph;
        }

        static async Task<IGraph> LoadCatalogPage(IStorage storage, Uri pageUri)
        {
            string json = await storage.LoadString(pageUri);
            IGraph graph = Utils.CreateGraph(json);
            return graph;
        }

        static async Task LoadCatalogItems(IStorage storage, IGraph graph)
        {
            IList<Uri> itemUris = new List<Uri>();

            IEnumerable<Triple> pages = graph.GetTriplesWithPredicateObject(graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogPage));

            foreach (Triple page in pages)
            {
                IEnumerable<Triple> items = graph.GetTriplesWithSubjectPredicate(page.Subject, graph.CreateUriNode(Schema.Predicates.CatalogItem));

                foreach (Triple item in items)
                {
                    itemUris.Add(((IUriNode)item.Object).Uri);
                }
            }

            IList<Task> tasks = new List<Task>();

            foreach (Uri itemUri in itemUris)
            {
                tasks.Add(storage.LoadString(itemUri));
            }

            await Task.WhenAll(tasks.ToArray());

            //TODO: if we have details at the package level and not inlined on a page we will merge them in here
        }

        static async Task<IGraph> LoadCatalogItem(IStorage storage, Uri itemUri)
        {
            string json = await storage.LoadString(itemUri);
            IGraph graph = Utils.CreateGraph(json);
            return graph;
        }

        //  Save implementation

        static async Task Save(IStorage storage, Uri registrationBaseAddress, IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration, int partitionSize, int packageCountThreshold)
        {
            IDictionary<string, IGraph> items = new Dictionary<string, IGraph>();

            foreach (RegistrationCatalogEntry value in registration.Values)
            {
                if (value != null)
                {
                    items.Add(value.ResourceUri, value.Graph);
                }
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

            //await graphPersistence.Initialize();

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
