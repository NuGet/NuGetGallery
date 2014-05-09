using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using VDS.RDF;

namespace Catalog.Maintenance
{
    public abstract class CatalogContainer
    {
        Uri _resourceUri;
        Uri _parent;

        public CatalogContainer(Uri resourceUri, Uri parent = null)
        {
            _resourceUri = resourceUri;
            _parent = parent;
        }

        protected abstract IEnumerable<Tuple<Uri, DateTime, int?>> GetItems();

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

            foreach (Tuple<Uri, DateTime, int?> item in GetItems())
            {
                INode itemNode = graph.CreateUriNode(item.Item1);

                graph.Assert(container, itemPredicate, itemNode);
                graph.Assert(itemNode, publishedPredicate, graph.CreateLiteralNode(item.Item2.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#dateTime")));
                if (item.Item3 != null)
                {
                    graph.Assert(itemNode, countPredicate, graph.CreateLiteralNode(item.Item3.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#integer")));
                }
            }

            JObject frame = context.GetJsonLdContext("context.ContainerFrame.json");

            frame["@type"] = "http://nuget.org/schema#Container";

            string content = Utils.CreateJson(graph, frame);

            return content;
        }
    }
}
