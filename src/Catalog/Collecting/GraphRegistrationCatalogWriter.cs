using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    class GraphRegistrationCatalogWriter : RegistrationCatalogWriter
    {
        Uri[] _propertiesToUpdate =
        {
            Schema.Predicates.CatalogCommitId,
            Schema.Predicates.CatalogTimeStamp,
            Schema.Predicates.CatalogCount
        };

        public GraphRegistrationCatalogWriter(Storage storage)
            : base(storage, null)
        {
            Graph = new Graph();
        }

        public IGraph Graph { get; private set; }
        public Uri ResourceUri { get; private set; }
        public Uri TypeUri { get; private set; }

        protected override async Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri)
        {
            await Task.Run(() =>
            {
                Utils.RemoveExistingProperties(Graph, graph, _propertiesToUpdate);

                Graph.Merge(graph, true);

                ResourceUri = resourceUri;
                TypeUri = typeUri;
            });
        }

        protected override Task<IGraph> LoadGraph(Uri resourceUri)
        {
            return Task.FromResult(Graph);
        }
        protected override Uri CreatePageUri(Uri baseAddress, string relativeAddress)
        {
            return new Uri(Storage.BaseAddress, "index.json#" + relativeAddress);
        }
    }
}
