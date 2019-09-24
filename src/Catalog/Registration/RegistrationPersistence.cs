// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationPersistence : IRegistrationPersistence
    {
        private readonly Uri _registrationUri;
        private readonly int _packageCountThreshold;
        private readonly int _partitionSize;
        private readonly RecordingStorage _storage;
        private readonly RegistrationMakerCatalogItem.PostProcessGraph _postProcessGraph;
        private readonly Uri _registrationBaseAddress;
        private readonly Uri _contentBaseAddress;
        private readonly Uri _galleryBaseAddress;
        private readonly bool _forcePackagePathProviderForIcons;

        public RegistrationPersistence(
            StorageFactory storageFactory, 
            RegistrationMakerCatalogItem.PostProcessGraph postProcessGraph, 
            RegistrationKey registrationKey, 
            int partitionSize, 
            int packageCountThreshold, 
            Uri contentBaseAddress, 
            Uri galleryBaseAddress,
            bool forcePackagePathProviderForIcons)
        {
            _storage = new RecordingStorage(storageFactory.Create(registrationKey.ToString()));
            _postProcessGraph = postProcessGraph;
            _registrationUri = _storage.ResolveUri("index.json");
            _packageCountThreshold = packageCountThreshold;
            _partitionSize = partitionSize;
            _registrationBaseAddress = storageFactory.BaseAddress;
            _contentBaseAddress = contentBaseAddress;
            _galleryBaseAddress = galleryBaseAddress;
            _forcePackagePathProviderForIcons = forcePackagePathProviderForIcons;
        }

        public Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load(CancellationToken cancellationToken)
        {
            return Load(_storage, _registrationUri, cancellationToken);
        }

        public async Task Save(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration, CancellationToken cancellationToken)
        {
            await Save(_storage, _postProcessGraph, _registrationBaseAddress, registration, _partitionSize, _packageCountThreshold, _contentBaseAddress, _galleryBaseAddress, _forcePackagePathProviderForIcons, cancellationToken);

            await Cleanup(_storage, cancellationToken);
        }

        //  Load implementation

        private static async Task<IDictionary<RegistrationEntryKey, RegistrationCatalogEntry>> Load(IStorage storage, Uri resourceUri, CancellationToken cancellationToken)
        {
            IGraph graph = await LoadCatalog(storage, resourceUri, cancellationToken);

            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources = GetResources(graph);

            Trace.TraceInformation("RegistrationPersistence.Load: resourceUri = {0}, resources = {1}", resourceUri, resources.Count);

            return resources;
        }

        private static IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> GetResources(IGraph graph)
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

        private static IList<Uri> ListExistingItems(TripleStore store)
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

        private static void AddExistingItem(IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> resources, TripleStore store, Uri catalogEntry)
        {
            Trace.TraceInformation("RegistrationPersistence.AddExistingItem: catalogEntry = {0}", catalogEntry);

            SparqlParameterizedString sparql = new SparqlParameterizedString
            {
                CommandText = Utils.GetResource("sparql.ConstructCatalogEntryGraph.rq")
            };

            sparql.SetUri("catalogEntry", catalogEntry);

            IGraph graph = SparqlHelpers.Construct(store, sparql.ToString());

            resources.Add(RegistrationCatalogEntry.Promote(
                catalogEntry.AbsoluteUri,
                graph,
                shouldInclude: (k, u, g) => true,
                isExistingItem: true));
        }

        private static async Task<IGraph> LoadCatalog(IStorage storage, Uri resourceUri, CancellationToken cancellationToken)
        {
            string json = await storage.LoadStringAsync(resourceUri, cancellationToken);

            IGraph graph = Utils.CreateGraph(resourceUri, json);

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

        private static async Task<IGraph> LoadCatalogPage(IStorage storage, Uri pageUri, CancellationToken cancellationToken)
        {
            string json = await storage.LoadStringAsync(pageUri, cancellationToken);
            IGraph graph = Utils.CreateGraph(pageUri, json);
            return graph;
        }

        //  Save implementation
        private static async Task Save(
            IStorage storage, 
            RegistrationMakerCatalogItem.PostProcessGraph preprocessGraph, 
            Uri registrationBaseAddress, 
            IDictionary<RegistrationEntryKey, RegistrationCatalogEntry> registration, 
            int partitionSize, 
            int packageCountThreshold, 
            Uri contentBaseAddress, 
            Uri galleryBaseAddress,
            bool forcePackagePathProviderForIcons,
            CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.Save");

            var items = registration.Values.Where(v => v != null).ToList();

            if (items.Count == 0)
            {
                return;
            }

            if (items.Count < packageCountThreshold)
            {
                await SaveSmallRegistration(storage, preprocessGraph, registrationBaseAddress, items, partitionSize, contentBaseAddress, galleryBaseAddress, forcePackagePathProviderForIcons, cancellationToken);
            }
            else
            {
                await SaveLargeRegistration(storage, preprocessGraph, registrationBaseAddress, items, partitionSize, contentBaseAddress, galleryBaseAddress, forcePackagePathProviderForIcons, cancellationToken);
            }
        }

        private static async Task SaveSmallRegistration(
            IStorage storage, 
            RegistrationMakerCatalogItem.PostProcessGraph preprocessGraph, 
            Uri registrationBaseAddress, 
            IList<RegistrationCatalogEntry> items, 
            int partitionSize, Uri contentBaseAddress, 
            Uri galleryBaseAddress,
            bool forcePackagePathProviderForIcons,
            CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.SaveSmallRegistration");

            SingleGraphPersistence graphPersistence = new SingleGraphPersistence(storage);

            //await graphPersistence.Initialize();

            await SaveRegistration(storage, preprocessGraph, registrationBaseAddress, items, null, graphPersistence, partitionSize, contentBaseAddress, galleryBaseAddress, forcePackagePathProviderForIcons, cancellationToken);

            // now the commit has happened the graphPersistence.Graph should contain all the data

            JObject frame = (new CatalogContext()).GetJsonLdContext("context.Registration.json", graphPersistence.TypeUri);
            StorageContent content = new JTokenStorageContent(Utils.CreateJson(graphPersistence.Graph, frame), "application/json", "no-store");
            await storage.SaveAsync(graphPersistence.ResourceUri, content, cancellationToken);
        }

        private static async Task SaveLargeRegistration(
            IStorage storage, 
            RegistrationMakerCatalogItem.PostProcessGraph preprocessGraph, 
            Uri registrationBaseAddress, 
            IList<RegistrationCatalogEntry> items, 
            int partitionSize, 
            Uri contentBaseAddress, 
            Uri galleryBaseAddress,
            bool forcePackagePathProviderForIcons,
            CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.SaveLargeRegistration: registrationBaseAddress = {0} items: {1}", registrationBaseAddress, items.Count);

            IList<Uri> cleanUpList = new List<Uri>();

            await SaveRegistration(storage, preprocessGraph, registrationBaseAddress, items, cleanUpList, null, partitionSize, contentBaseAddress, galleryBaseAddress, forcePackagePathProviderForIcons, cancellationToken);
        }

        private static async Task SaveRegistration(
            IStorage storage, 
            RegistrationMakerCatalogItem.PostProcessGraph postProcessGraph, 
            Uri registrationBaseAddress, 
            IList<RegistrationCatalogEntry> items, 
            IList<Uri> cleanUpList, 
            SingleGraphPersistence graphPersistence, 
            int partitionSize, 
            Uri contentBaseAddress, 
            Uri galleryBaseAddress,
            bool forcePackagePathProviderForIcons,
            CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.SaveRegistration: registrationBaseAddress = {0} items: {1}", registrationBaseAddress, items.Count);

            using (RegistrationMakerCatalogWriter writer = new RegistrationMakerCatalogWriter(storage, partitionSize, cleanUpList, graphPersistence))
            {
                foreach (var item in items)
                {
                    writer.Add(new RegistrationMakerCatalogItem(new Uri(item.ResourceUri), item.Graph, registrationBaseAddress, item.IsExistingItem, postProcessGraph, forcePackagePathProviderForIcons, contentBaseAddress, galleryBaseAddress));
                }
                await writer.Commit(DateTime.UtcNow, null, cancellationToken);
            }
        }

        private static async Task Cleanup(RecordingStorage storage, CancellationToken cancellationToken)
        {
            Trace.TraceInformation("RegistrationPersistence.Cleanup");

            IList<Task> tasks = new List<Task>();
            foreach (Uri loaded in storage.Loaded)
            {
                if (!storage.Saved.Contains(loaded))
                {
                    tasks.Add(storage.DeleteAsync(loaded, cancellationToken));
                }
            }
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }
    }
}