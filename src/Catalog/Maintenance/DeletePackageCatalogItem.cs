using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using NuGet.Services.Metadata.Catalog.GalleryIntegration;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class DeletePackageCatalogItem : CatalogItem
    {
        string _id;
        string _version;
        string _galleryKey;

        public DeletePackageCatalogItem(string id, string version) : this(id, version, null) { }

        public DeletePackageCatalogItem(string id, string version, string galleryKey)
        {
            _id = id;
            _version = version;
            _galleryKey = galleryKey;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));
            graph.NamespaceMap.AddNamespace("gallery", new Uri("http://nuget.org/gallery#"));

            INode subject = graph.CreateUriNode(new Uri(GetBaseAddress() + GetItemIdentity()));

            graph.Assert(subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:DeletePackage"));
            graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(_id));
            graph.Assert(subject, graph.CreateUriNode("nuget:version"), graph.CreateLiteralNode(_version));
            if (!String.IsNullOrEmpty(_galleryKey))
            {
                graph.Assert(subject, graph.CreateUriNode("gallery:key"), graph.CreateLiteralNode(_galleryKey));
            }

            JObject frame = context.GetJsonLdContext("context.DeletePackage.json", GetItemType());

            StorageContent content = new StringStorageContent(Utils.CreateJson(graph, frame), "application/json");

            return content;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            Graph graph = new Graph();

            if (!String.IsNullOrEmpty(_galleryKey))
            {
                INode subject = graph.CreateUriNode(resourceUri);
                INode galleryKeyPredicate = graph.CreateUriNode(Schema.Predicates.GalleryKey);

                graph.Assert(subject, galleryKeyPredicate, graph.CreateLiteralNode(_galleryKey, Schema.DataTypes.Integer));
            }

            return graph;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.DeletePackage;
        }

        protected override string GetItemIdentity()
        {
            return "delete/" + _id.ToLowerInvariant() + "." + _version.ToLowerInvariant();
        }
    }
}
