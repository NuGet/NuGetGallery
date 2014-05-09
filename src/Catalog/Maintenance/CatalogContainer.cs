using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace Catalog.Maintenance
{
    abstract class CatalogContainer
    {
        Uri _resourceUri;
        Uri _parent;

        public CatalogContainer(Uri resourceUri, Uri parent = null)
        {
            _resourceUri = resourceUri;
            _parent = parent;
        }

        protected abstract IDictionary<Uri, Tuple<DateTime, int?>> GetItems();

        public string CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode container = graph.CreateUriNode(_resourceUri);

            graph.Assert(container, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Container"));

            if (_parent != null)
            {
                graph.Assert(container, graph.CreateUriNode("nuget:parent"), graph.CreateUriNode(_parent));
            }

            INode itemPredicate = graph.CreateUriNode("nuget:item");
            INode publishedPredicate = graph.CreateUriNode("nuget:published");
            INode countPredicate = graph.CreateUriNode("nuget:count");

            foreach (KeyValuePair<Uri, Tuple<DateTime, int?>> item in GetItems())
            {
                INode itemNode = graph.CreateUriNode(item.Key);

                graph.Assert(container, itemPredicate, itemNode);
                graph.Assert(itemNode, publishedPredicate, graph.CreateLiteralNode(item.Value.Item1.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#dateTime")));
                if (item.Value.Item2 != null)
                {
                    graph.Assert(itemNode, countPredicate, graph.CreateLiteralNode(item.Value.Item2.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#integer")));
                }
            }

            JObject frame = context.GetJsonLdContext("context.ContainerFrame.json");

            frame["@type"] = "http://nuget.org/schema#Container";

            string content = Utils.CreateJson(graph, frame);

            return content;
        }

        protected static void Load(IDictionary<Uri, Tuple<DateTime, int?>> items, string content)
        {
            IGraph graph = Utils.CreateGraph(content);

            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));
            INode itemPredicate = graph.CreateUriNode("nuget:item");
            INode publishedPredicate = graph.CreateUriNode("nuget:published");
            INode countPredicate = graph.CreateUriNode("nuget:count");

            foreach (Triple itemTriple in graph.GetTriplesWithPredicate(itemPredicate))
            {
                Uri itemUri = ((IUriNode)itemTriple.Object).Uri;

                Triple publishedTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, publishedPredicate).First();
                DateTime published = DateTime.Parse(((ILiteralNode)publishedTriple.Object).Value);

                IEnumerable<Triple> countTriples = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, countPredicate);

                int? count = null;
                if (countTriples.Count() > 0)
                {
                    Triple countTriple = countTriples.First();
                    count = int.Parse(((ILiteralNode)countTriple.Object).Value);
                }

                items.Add(itemUri, new Tuple<DateTime, int?>(published, count));
            }
        }
    }
}
