// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
        Uri _contentBaseAddress;

        public RegistrationPersistence(StorageFactory storageFactory, RegistrationKey registrationKey, int partitionSize, int packageCountThreshold, Uri contentBaseAddress)
        {
            _storage = new RecordingStorage(storageFactory.Create(registrationKey.ToString()));
            _registrationUri = _storage.ResolveUri("index.json");
            _packageCountThreshold = packageCountThreshold;
            _partitionSize = partitionSize;
            _registrationBaseAddress = storageFactory.BaseAddress;
            _contentBaseAddress = contentBaseAddress;
        }

        public Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load(CancellationToken cancellationToken)
        {
            return Load(_storage, _registrationUri, cancellationToken);
        }

        public async Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration, CancellationToken cancellationToken)
        {
            await Save(_storage, _registrationBaseAddress, registration, _partitionSize, _packageCountThreshold, _contentBaseAddress, cancellationToken);

            await Cleanup(_storage, cancellationToken);
        }

        //  Load implementation

        static async Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load(IStorage storage, Uri resourceUri, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.Load: resourceUri = {0}", resourceUri);

            IGraph graph = await LoadCatalog(storage, resourceUri,cancellationToken);

            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources = GetResources(graph);

            Trace.TraceInformation("RegistrationPersistence.Load: resources = {0}", resources.Count);

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

            Trace.TraceInformation("RegistrationPersistence.ListExistingItems results = {0}", results.Count);

            return results;
        }

        static void AddExistingItem(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources, TripleStore store, Uri catalogEntry)
        {
            Trace.TraceInformation("RegistrationPersistence.AddExistingItem: catalogEntry = {0}", catalogEntry);

            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.ConstructCatalogEntryGraph.rq");
            sparql.SetUri("catalogEntry", catalogEntry);

            IGraph graph = SparqlHelpers.Construct(store, sparql.ToString());

            resources.Add(RegistrationCatalogEntry.Promote(catalogEntry.AbsoluteUri, graph, isExistingItem: true));
        }

        static async Task<IGraph> LoadCatalog(IStorage storage, Uri resourceUri, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.LoadCatalog: resourceUri = {0}", resourceUri);

            string json = await storage.LoadString(resourceUri, cancellationToken);

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

                //  note that this is explicit Uri comparison and deliberately ignores differences in the fragment
                
                if (pageUri != resourceUri)
                {
                    tasks.Add(LoadCatalogPage(storage, pageUri, cancellationToken));
                }
            }

            await Task.WhenAll(tasks.ToArray());

            foreach (Task<IGraph> task in tasks)
            {
                graph.Merge(task.Result, false);
            }

            return graph;
        }

        static async Task<IGraph> LoadCatalogPage(IStorage storage, Uri pageUri, CancellationToken cancellationToken)
        {
            string json = await storage.LoadString(pageUri, cancellationToken);
            IGraph graph = Utils.CreateGraph(json);
            return graph;
        }
        
        //  Save implementation

        static async Task Save(IStorage storage, Uri registrationBaseAddress, IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration, int partitionSize, int packageCountThreshold, Uri contentBaseAddress, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.Save");

            var items = registration.Values.Where(v => v != null).ToList();

            if (items.Count == 0)
            {
                return;
            }

            if (items.Count < packageCountThreshold)
            {
                await SaveSmallRegistration(storage, registrationBaseAddress, items, partitionSize, contentBaseAddress, cancellationToken);
            }
            else
            {
                await SaveLargeRegistration(storage, registrationBaseAddress, items, partitionSize, contentBaseAddress, cancellationToken);
            }
        }

        static async Task SaveSmallRegistration(IStorage storage, Uri registrationBaseAddress, IList<RegistrationCatalogEntry> items, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.SaveSmallRegistration");

            SingleGraphPersistence graphPersistence = new SingleGraphPersistence(storage);

            //await graphPersistence.Initialize();

            await SaveRegistration(storage, registrationBaseAddress, items, null, graphPersistence, partitionSize, contentBaseAddress, cancellationToken);

            // now the commit has happened the graphPersistence.Graph should contain all the data

            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", graphPersistence.TypeUri);
            StorageContent content = new JTokenStorageContent(Utils.CreateJson(graphPersistence.Graph, frame), "application/json", "no-store");
            await storage.Save(graphPersistence.ResourceUri, content, cancellationToken);
        }

        static async Task SaveLargeRegistration(IStorage storage, Uri registrationBaseAddress, IList<RegistrationCatalogEntry> items, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.SaveLargeRegistration: registrationBaseAddress = {0} items: {1}", registrationBaseAddress, items.Count);

            IList<Uri> cleanUpList = new List<Uri>();

            await SaveRegistration(storage, registrationBaseAddress, items, cleanUpList, null, partitionSize, contentBaseAddress, cancellationToken);
        }

        static async Task SaveRegistration(IStorage storage, Uri registrationBaseAddress, IList<RegistrationCatalogEntry> items, IList<Uri> cleanUpList, SingleGraphPersistence graphPersistence, int partitionSize, Uri contentBaseAddress, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.SaveRegistration: registrationBaseAddress = {0} items: {1}", registrationBaseAddress, items.Count);

            using (RegistrationMakerCatalogWriter writer = new RegistrationMakerCatalogWriter(storage, partitionSize, cleanUpList, graphPersistence))
            {
                foreach (var item in items)
                {
                    writer.Add(new RegistrationMakerCatalogItem(new Uri(item.ResourceUri), item.Graph, registrationBaseAddress, item.IsExistingItem, contentBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow, null, cancellationToken);
            }
        }

        static async Task Cleanup(RecordingStorage storage, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.Cleanup");

            IList<Task> tasks = new List<Task>();
            foreach (Uri loaded in storage.Loaded)
            {
                if (!storage.Saved.Contains(loaded))
                {
                    tasks.Add(storage.Delete(loaded, cancellationToken));
                }
            }
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }
    }
}
