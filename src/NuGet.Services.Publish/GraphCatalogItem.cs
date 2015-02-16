using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using VDS.RDF;

namespace NuGet.Services.Publish
{
    public class GraphCatalogItem : AppendOnlyCatalogItem
    {
        JObject _nuspec;
        Uri _itemType;
        Uri _nupkgAddress;
        long _packageSize;
        string _packageHash;
        PublicationDetails _publicationDetails;

        Guid _catalogItemId;
        JObject _context;

        public GraphCatalogItem(JObject nuspec, Uri itemType, Uri nupkgAddress, long packageSize, string packageHash, PublicationDetails publicationDetails)
        {
            _nuspec = nuspec;
            _itemType = itemType;
            _nupkgAddress = nupkgAddress;
            _packageSize = packageSize;
            _packageHash = packageHash;
            _publicationDetails = publicationDetails;

            _catalogItemId = Guid.NewGuid();

            _context = LoadContext("context.catalog.json");
            _context["@type"] = GetItemType().AbsoluteUri;
        }

        static JObject LoadContext(string name)
        {
            string assName = Assembly.GetExecutingAssembly().GetName().Name;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assName + "." + name))
            {
                string json = new StreamReader(stream).ReadToEnd();
                return JObject.Parse(json);
            }
        }

        public override Uri GetItemType()
        {
            return _itemType;
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
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(Schema.DataTypes.Permalink));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogTimeStamp), catalogEntry.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogCommitId), catalogEntry.CreateLiteralNode(CommitId.ToString()));

            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.PackageContent), catalogEntry.CreateUriNode(_nupkgAddress));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.PackageSize), catalogEntry.CreateLiteralNode(_packageSize.ToString()));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.PackageHash), catalogEntry.CreateLiteralNode(_packageHash));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Published), catalogEntry.CreateLiteralNode(_publicationDetails.Published.ToString("O"), Schema.DataTypes.DateTime));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.UserName), catalogEntry.CreateLiteralNode(_publicationDetails.UserName));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Tenant), catalogEntry.CreateLiteralNode(_publicationDetails.TenantName));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.UserId), catalogEntry.CreateLiteralNode(_publicationDetails.UserId));
            catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.TenantId), catalogEntry.CreateLiteralNode(_publicationDetails.TenantId));

            //  add the nuspec.json metadata

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

            //  create JSON content

            string json = Utils.CreateJson(catalogEntry, _context);

            StorageContent content = new StringStorageContent(json, "application/json", "no-store");

            return content;
        }
    }
}
