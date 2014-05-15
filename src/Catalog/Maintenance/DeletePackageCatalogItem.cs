using Newtonsoft.Json.Linq;
using System;
using VDS.RDF;

namespace Catalog.Maintenance
{
    public class DeletePackageCatalogItem : CatalogItem
    {
        string _id;

        public DeletePackageCatalogItem(string id)
        {
            _id = id;
        }

        public override string CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode subject = graph.CreateUriNode(GetBaseAddress() + GetItemName());

            graph.Assert(subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:DeletePackage"));
            graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(_id));

            JObject frame = context.GetJsonLdContext("context.DeletePackageFrame.json", GetItemType());

            string content = Utils.CreateJson(graph, frame);

            return content;
        }

        public override string GetItemType()
        {
            return "http://nuget.org/schema#DeletePackage";
        }

        protected override string GetItemName()
        {
            return "delete/" + _id;
        }
    }
}
