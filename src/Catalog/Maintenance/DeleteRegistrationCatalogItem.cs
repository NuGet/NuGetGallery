using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
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

        public override StorageContent CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            INode subject = graph.CreateUriNode(new Uri(GetBaseAddress() + GetItemIdentity()));

            graph.Assert(subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:DeleteRegistration"));
            graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(_id));

            JObject frame = context.GetJsonLdContext("context.DeletePackage.json", GetItemType());

            StorageContent content = new StringStorageContent(Utils.CreateJson(graph, frame), "application/json");

            return content;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.DeleteRegistration;
        }

        protected override string GetItemIdentity()
        {
            return "delete/" + _id;
        }
    }
}
