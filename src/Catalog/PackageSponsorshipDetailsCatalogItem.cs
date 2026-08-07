// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// A version-less catalog leaf carrying ID-level (package registration) metadata. This is the
    /// server-side counterpart to <c>PackageDetailsCatalogLeaf</c>'s ID-level attributes and is
    /// emitted once per changed package registration by the db2catalog registration lane.
    /// </summary>
    public class PackageSponsorshipDetailsCatalogItem : AppendOnlyCatalogItem
    {
        private readonly string _id;
        private readonly IReadOnlyList<string> _sponsorshipUrls;

        public PackageSponsorshipDetailsCatalogItem(string id, IReadOnlyList<string> sponsorshipUrls)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The package id must not be null or empty.", nameof(id));
            }

            _id = id;
            _sponsorshipUrls = sponsorshipUrls ?? Array.Empty<string>();
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageSponsorshipDetails;
        }

        protected override string GetItemIdentity()
        {
            return _id.ToLowerInvariant();
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            using (IGraph graph = new Graph())
            {
                INode entry = graph.CreateUriNode(GetItemAddress());

                // catalog infrastructure fields
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(GetItemType()));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.Permalink));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp), graph.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.CatalogCommitId), graph.CreateLiteralNode(CommitId.ToString()));

                // ID-level fields
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(_id));
                graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.OriginalId), graph.CreateLiteralNode(_id));

                foreach (var sponsorshipUrl in _sponsorshipUrls.Where(url => !string.IsNullOrEmpty(url)))
                {
                    graph.Assert(entry, graph.CreateUriNode(Schema.Predicates.SponsorshipUrl), graph.CreateLiteralNode(sponsorshipUrl));
                }

                // create JSON content
                JObject frame = context.GetJsonLdContext("context.PackageSponsorshipDetails.json", GetItemType());
                StorageContent content = new StringStorageContent(
                    Utils.CreateArrangedJson(graph, frame),
                    "application/json",
                    context.ItemCacheControl);

                return content;
            }
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            var resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            var graph = new Graph();

            var subject = graph.CreateUriNode(resourceUri);

            graph.Assert(subject, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(_id));

            return graph;
        }
    }
}
