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

            INode subject = graph.CreateUriNode(new Uri(GetBaseAddress() + GetItemIdentity()));

            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.DeletePackage));
            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.PackageId), graph.CreateLiteralNode(_id));

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
