using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    class GraphCatalogWriter : AppendOnlyCatalogWriter
    {
        Uri[] _propertiesToUpdate =
        {
            Schema.Predicates.CatalogCommitId,
            Schema.Predicates.CatalogTimestamp,
            Schema.Predicates.CatalogCount
        };

        public GraphCatalogWriter(Storage storage, int maxPageSize = 1000, bool append = true)
            : base(storage, maxPageSize, append)
        {
            Graph = new Graph();
        }

        public IGraph Graph { get; private set; }
        public Uri ResourceUri { get; private set; }
        public Uri TypeUri { get; private set; }

        protected override async Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri, bool last)
        {
            await Task.Run(() =>
            {
                RemoveExistingProperties(Graph, graph, _propertiesToUpdate);

                Graph.Merge(graph, true);

                ResourceUri = resourceUri;
                TypeUri = typeUri;
            });
        }

        protected override Task<IGraph> LoadGraph(Uri resourceUri)
        {
            return Task.FromResult(Graph);
        }
        protected override Uri CreatePageUri(Uri baseAddress, int pageNumber)
        {
            string relativeAddress = string.Format("index.json#page{0}", pageNumber);
            return new Uri(baseAddress, relativeAddress);
        }

        //  where the property exists on the graph being merged in remove it from the existing graph

        static void RemoveExistingProperties(IGraph existingGraph, IGraph graphToMerge, Uri[] properties)
        {
            foreach (Uri property in properties)
            {
                foreach (Triple t1 in graphToMerge.GetTriplesWithPredicate(graphToMerge.CreateUriNode(property)))
                {
                    INode subject = t1.Subject.CopyNode(existingGraph);
                    INode predicate = t1.Predicate.CopyNode(existingGraph);

                    IList<Triple> retractList = new List<Triple>(existingGraph.GetTriplesWithSubjectPredicate(subject, predicate));
                    foreach (Triple t2 in retractList)
                    {
                        existingGraph.Retract(t2);
                    }
                }
            }
        }
    }
}
