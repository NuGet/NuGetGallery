using System;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class SingleGraphPersistence : ICatalogGraphPersistence
    {
        Uri[] _propertiesToUpdate =
        {
            Schema.Predicates.CatalogCommitId,
            Schema.Predicates.CatalogTimeStamp,
            Schema.Predicates.CatalogCount
        };

        Uri _baseAddress;

        public SingleGraphPersistence(Uri baseAddress)
        {
            Graph = new Graph();
            _baseAddress = baseAddress;
        }

        public IGraph Graph { get; private set; }
        public Uri ResourceUri { get; private set; }
        public Uri TypeUri { get; private set; }

        public async Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri)
        {
            await Task.Run(() =>
            {
                Utils.RemoveExistingProperties(Graph, graph, _propertiesToUpdate);

                Graph.Merge(graph, true);

                ResourceUri = resourceUri;
                TypeUri = typeUri;
            });
        }

        public Task<IGraph> LoadGraph(Uri resourceUri)
        {
            return Task.FromResult(Graph);
        }

        public Uri CreatePageUri(Uri baseAddress, string relativeAddress)
        {
            return new Uri(_baseAddress, "index.json#" + relativeAddress);
        }
    }
}
