using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogItem : CatalogItem
    {
        string _version;
        Uri _itemUri;

        //public RegistrationCatalogItem(JObject obj)
        //{
        //    _obj = obj;
        //    _version = obj["nuget:version"].ToString();
        //    _itemUri = obj["url"].ToObject<Uri>();
        //}

        public RegistrationCatalogItem(IGraph graph)
        {
            INode resourceNode = graph.GetTriplesWithPredicateObject(
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.Package)).First().Subject;

            INode versionNode = graph.GetTriplesWithSubjectPredicate(
                resourceNode,
                graph.CreateUriNode(Schema.Predicates.Version)).First().Object;

            _version = versionNode.ToString();
            _itemUri = ((IUriNode)resourceNode).Uri;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            return null;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        protected override string GetItemIdentity()
        {
            return _version;
        }

        public override Uri GetItemAddress()
        {
            return _itemUri;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            IGraph content = new Graph();

            INode resourceNode = content.CreateUriNode(GetItemAddress());

            content.Assert(
                resourceNode,
                content.CreateUriNode(Schema.Predicates.Version),
                content.CreateLiteralNode(_version));

            return content;
        }
    }
}
