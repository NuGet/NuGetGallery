// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class SingleGraphPersistence : ICatalogGraphPersistence
    {
        Uri[] _propertiesToUpdate =
        {
            Schema.Predicates.CatalogCommitId,
            Schema.Predicates.CatalogTimeStamp,
            Schema.Predicates.CatalogCount
        };

        IStorage _storage;
        IGraph _initialGraph;

        public SingleGraphPersistence(IStorage storage)
        {
            _storage = storage;
            Graph = new Graph();
        }

        public IGraph Graph { get; private set; }
        public Uri ResourceUri { get; private set; }
        public Uri TypeUri { get; private set; }

        public async Task Initialize(CancellationToken cancellationToken)
        {
            Uri rootUri = _storage.ResolveUri("index.json");

            string json = await _storage.LoadStringAsync(rootUri, cancellationToken);

            if (json != null)
            {
                _initialGraph = Utils.CreateGraph(rootUri, json);
            }
            else
            {
                _initialGraph = null;
            }
        }

        public async Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                Utils.RemoveExistingProperties(Graph, graph, _propertiesToUpdate);

                Graph.Merge(graph, true);

                ResourceUri = resourceUri;
                TypeUri = typeUri;
            }, cancellationToken);
        }

        public Task<IGraph> LoadGraph(Uri resourceUri, CancellationToken cancellationToken)
        {
            return Task.FromResult(_initialGraph);
        }

        public Uri CreatePageUri(Uri baseAddress, string relativeAddress)
        {
            return new Uri(_storage.BaseAddress, "index.json#" + relativeAddress);
        }
    }
}