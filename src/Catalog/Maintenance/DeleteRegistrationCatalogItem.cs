using Newtonsoft.Json.Linq;
using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class DeleteRegistrationCatalogItem : CatalogItem
    {
        string _id;

        public DeleteRegistrationCatalogItem(string id)
        {
            _id = id;
        }

        public override string CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode subject = graph.CreateUriNode(new Uri(GetBaseAddress() + GetItemIdentity()));

            graph.Assert(subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:DeleteRegistration"));
            graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(_id));

            JObject frame = context.GetJsonLdContext("context.DeletePackage.json", GetItemType());

            string content = Utils.CreateJson(graph, frame);

            return content;
        }

        public override Uri GetItemType()
        {
            return Constants.DeleteRegistration;
        }

        protected override string GetItemIdentity()
        {
            return "delete/" + _id;
        }
    }
}
