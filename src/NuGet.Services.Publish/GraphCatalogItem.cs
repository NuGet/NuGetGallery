using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Publish
{
    public class GraphCatalogItem : AppendOnlyCatalogItem
    {
        JObject _nuspec;
        Uri _itemType;
        PublicationDetails _publicationDetails;

        Guid _catalogItemId;
        JObject _context;

        string _id;
        string _version;

        public GraphCatalogItem(JObject nuspec, Uri itemType, PublicationDetails publicationDetails)
        {
            _nuspec = nuspec;
            _itemType = itemType;
            _publicationDetails = publicationDetails;

            _catalogItemId = Guid.NewGuid();

            _context = ServiceHelpers.LoadContext("context.catalog.json");
            _context["@type"] = GetItemType().AbsoluteUri;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDetails;
        }

        protected override string GetItemIdentity()
        {
            return _catalogItemId.ToString().ToLowerInvariant();
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            IGraph catalogEntry = new Graph();

            INode catalogEntrySubject = catalogEntry.CreateUriNode(GetItemAddress());

            //  catalog infrastructure fields

            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(GetItemType()));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(_itemType));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(Schema.DataTypes.Permalink));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogTimeStamp), catalogEntry.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogCommitId), catalogEntry.CreateLiteralNode(CommitId.ToString()));

            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Published), catalogEntry.CreateLiteralNode(_publicationDetails.Published.ToString("O"), Schema.DataTypes.DateTime));

            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.TenantId), catalogEntry.CreateLiteralNode(_publicationDetails.TenantId));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Tenant), catalogEntry.CreateLiteralNode(_publicationDetails.TenantName));

            Uri ownerUri = _publicationDetails.Owner.GetUri(GetItemAddress());
            INode ownerSubject = catalogEntry.CreateUriNode(ownerUri);

            catalogEntry.Assert(ownerSubject, catalogEntry.CreateUriNode(Schema.Predicates.NameIdentifier), catalogEntry.CreateLiteralNode(_publicationDetails.Owner.NameIdentifier));
            catalogEntry.Assert(ownerSubject, catalogEntry.CreateUriNode(Schema.Predicates.Name), catalogEntry.CreateLiteralNode(_publicationDetails.Owner.Name));
            catalogEntry.Assert(ownerSubject, catalogEntry.CreateUriNode(Schema.Predicates.GivenName), catalogEntry.CreateLiteralNode(_publicationDetails.Owner.GivenName));
            catalogEntry.Assert(ownerSubject, catalogEntry.CreateUriNode(Schema.Predicates.Surname), catalogEntry.CreateLiteralNode(_publicationDetails.Owner.Surname));
            catalogEntry.Assert(ownerSubject, catalogEntry.CreateUriNode(Schema.Predicates.Iss), catalogEntry.CreateLiteralNode(_publicationDetails.Owner.Iss));

            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Owner), ownerSubject);

            //  visibility

            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Visibility), catalogEntry.CreateLiteralNode(_publicationDetails.Visibility.Visibility.ToString()));

            switch (_publicationDetails.Visibility.Visibility)
            {
                case PublicationVisibility.VisibilityScope.Organization:
                    catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Organization), catalogEntry.CreateLiteralNode(_publicationDetails.Visibility.Organization));
                    break;
                case PublicationVisibility.VisibilityScope.Subscription:
                    catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Subscription), catalogEntry.CreateLiteralNode(_publicationDetails.Visibility.Subscription));
                    break;
            }

            //  add the nuspec metadata

            Uri nuspecSubject = _nuspec["@id"].ToObject<Uri>();
            IGraph nuspecGraph = Utils.CreateGraph(_nuspec);

            //  Any statements made about this @id in the nuspec we want to make about the catalog items @id
            //  - catalog readers can then apply this logic in reverse
            //  - by so doing the catalog entry becomes an audit entry for the data

            catalogEntry.Merge(nuspecGraph, false);

            foreach (Triple triple in catalogEntry.GetTriplesWithSubject(catalogEntry.CreateUriNode(nuspecSubject)))
            {
                catalogEntry.Assert(catalogEntrySubject, triple.Predicate.CopyNode(catalogEntry), triple.Object.CopyNode(catalogEntry));
            }

            GraphHelpers.MaterializeInference(catalogEntry);

            SetIdVersionFromGraph(catalogEntry);

            //  create JSON content

            string json = Utils.CreateJson(catalogEntry, _context);

            StorageContent content = new StringStorageContent(json, "application/json", "no-store");

            return content;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            Graph graph = new Graph();

            INode subject = graph.CreateUriNode(resourceUri);

            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            if (_id != null)
            {
                graph.Assert(subject, idPredicate, graph.CreateLiteralNode(_id));
            }

            if (_version != null)
            {
                graph.Assert(subject, versionPredicate, graph.CreateLiteralNode(_version));
            }

            return graph;
        }

        void SetIdVersionFromGraph(IGraph graph)
        {
            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
            Triple id = graph.GetTriplesWithSubjectPredicate(resource.Subject, idPredicate).FirstOrDefault();
            if (id != null)
            {
                _id = ((ILiteralNode)id.Object).Value;
            }

            Triple version = graph.GetTriplesWithSubjectPredicate(resource.Subject, versionPredicate).FirstOrDefault();
            if (version != null)
            {
                _version = ((ILiteralNode)version.Object).Value;
            }
        }
    }
}
