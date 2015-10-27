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
        private readonly DateTime _published;

        public DeleteCatalogItem(string id, string version, DateTime published)
        {
            _id = id;
            _version = TryNormalize(version);
            _published = published;
        }

        private string TryNormalize(string version)
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
            return (_id + "." + _version).ToLowerInvariant();
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            using (IGraph graph = new Graph())
            {
                INode entry = graph.CreateUriNode(GetItemAddress());

                //  catalog infrastructure fields
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(GetItemType()));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Permalink));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp), graph.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.CatalogCommitId), graph.CreateLiteralNode(CommitId.ToString()));

                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Published), graph.CreateLiteralNode(_published.ToString("O"), Schema.DataTypes.DateTime));

                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(_id));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.OriginalId), graph.CreateLiteralNode(_id));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode(_version));

                SetIdVersionFromGraph(graph);

                //  create JSON content
                JObject frame = context.GetJsonLdContext("context.Catalog.json", GetItemType());
                StorageContent content = new StringStorageContent(Utils.CreateArrangedJson(graph, frame), "application/json", "no-store");
               
                return content;
            }
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            var resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            var graph = new Graph();

            var subject = graph.CreateUriNode(resourceUri);

            var idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            var versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

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

        private void SetIdVersionFromGraph(IGraph graph)
        {
            var resource = graph.GetTriplesWithPredicateObject(
                graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(GetItemType())).First();

            var id = graph.GetTriplesWithSubjectPredicate(
                resource.Subject, graph.CreateUriNode(Schema.Predicates.Id)).FirstOrDefault();
            if (id != null)
            {
                _id = ((ILiteralNode)id.Object).Value;
            }

            var version = graph.GetTriplesWithSubjectPredicate(
                resource.Subject, graph.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault();
            if (version != null)
            {
                _version = ((ILiteralNode)version.Object).Value;
            }
        }
    }
}