// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class DeleteCatalogItem : AppendOnlyCatalogItem
    {
        private string _id;
        private string _version;
        private DateTime _published;
        private Guid _catalogItemId;

        public DeleteCatalogItem(string id, string version, DateTime published)
        {
            _id = id;
            _version = ParseVersion(version);
            _published = published;

            _catalogItemId = Guid.NewGuid();
        }

        private string ParseVersion(string version)
        {
            SemanticVersion semVer;
            if (SemanticVersion.TryParse(version, out semVer))
            {
                return semVer.ToNormalizedString();
            }
            return version;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDelete;
        }

        protected override string GetItemIdentity()
        {
            return _catalogItemId.ToString().ToLowerInvariant();
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            using (IGraph catalogEntry = new Graph())
            {
                INode catalogEntrySubject = catalogEntry.CreateUriNode(GetItemAddress());

                //  catalog infrastructure fields
                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(GetItemType()));
                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(Schema.DataTypes.Permalink));
                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogTimeStamp), catalogEntry.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogCommitId), catalogEntry.CreateLiteralNode(CommitId.ToString()));

                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Published), catalogEntry.CreateLiteralNode(_published.ToString("O"), Schema.DataTypes.DateTime));

                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Id), catalogEntry.CreateLiteralNode(_id));
                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.OriginalId), catalogEntry.CreateLiteralNode(_id));
                catalogEntry.Assert(catalogEntrySubject, catalogEntry.CreateUriNode(Schema.Predicates.Version), catalogEntry.CreateLiteralNode(_version));

                SetIdVersionFromGraph(catalogEntry);

                //  create JSON content
                JObject frame = context.GetJsonLdContext("context.Catalog.json", GetItemType());
                StorageContent content = new StringStorageContent(Utils.CreateArrangedJson(catalogEntry, frame), "application/json", "no-store");
               
                return content;
            }
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