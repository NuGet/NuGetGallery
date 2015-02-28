using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog
{
    public class RegistrationPersistence
    {
        IList<Uri> _loaded;
        IList<Uri> _saved;
        Uri _registrationUri;
        int _packageCountThreshold;
        Storage _storage;

        public RegistrationPersistence(StorageFactory storageFactory, string id, int packageCountThreshold)
        {
            _storage = storageFactory.Create(id.ToLowerInvariant());
            _registrationUri = _storage.ResolveUri("index.json");
            _packageCountThreshold = packageCountThreshold;

            _loaded = new List<Uri>();
            _saved = new List<Uri>();
        }

        public Task<IDictionary<RegistrationKey, Tuple<string, IGraph>>> Load()
        {
            return Load(_storage, _registrationUri, _loaded);
        }

        public async Task Save(IDictionary<RegistrationKey, Tuple<string, IGraph>> resulting)
        {

            await Cleanup(_loaded, _saved);
        }

        //  Load implementation

        static async Task<IDictionary<RegistrationKey, Tuple<string, IGraph>>> Load(Storage storage, Uri resourceUri, IList<Uri> loaded)
        {
            IGraph graph = await LoadCatalog(storage, resourceUri, loaded);

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

        static async Task<IGraph> LoadCatalog(Storage storage, Uri resourceUri, IList<Uri> loaded)
        {
            loaded.Add(resourceUri);

            string json = await storage.LoadString(resourceUri);

            IGraph graph = Utils.CreateGraph(json);
            IEnumerable<Triple> pages = graph.GetTriplesWithPredicateObject(graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogPage));

            IList<Task<IGraph>> tasks = new List<Task<IGraph>>();

            foreach (Triple page in pages)
            {
                Uri pageUri = ((IUriNode)page.Subject).Uri;
                if (pageUri != resourceUri)
                {
                    tasks.Add(LoadCatalogPage(storage, pageUri, loaded));
                }
            }

            await Task.WhenAll(tasks.ToArray());

            foreach (Task<IGraph> task in tasks)
            {
                graph.Merge(task.Result, true);
            }

            return graph;
        }

        static async Task<IGraph> LoadCatalogPage(Storage storage, Uri pageUri, IList<Uri> loaded)
        {
            loaded.Add(pageUri);

            string json = await storage.LoadString(pageUri);
            IGraph graph = Utils.CreateGraph(json);
            return graph;
        }

        //  Save implementation

        static async Task Save(StorageFactory storageFactory, IDictionary<RegistrationKey, Tuple<string, IGraph>> resulting, IList<Uri> saved, int packageCountThreshold)
        {
            /*
            foreach (var item in resulting)
            {
                Console.WriteLine("Save: {0} : {1}", item.Key, item.Value.Item1);
            }

            //int count = resulting

            if (count < packageCountThreshold)
            {
                await SaveSmallRegistration(storage, storageFactory.BaseAddress, sortedGraphs, contentBaseAddress, partitionSize);
            }
            else
            {
                await SaveLargeRegistration(storage, storageFactory.BaseAddress, sortedGraphs, json, contentBaseAddress, partitionSize);
            }
            */
        }

        static async Task Cleanup(IList<Uri> loaded, IList<Uri> saved)
        {
        }
    }
}
