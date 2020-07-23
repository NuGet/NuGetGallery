// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class PackageCatalogItem : AppendOnlyCatalogItem
    {
        private string _id;
        private string _fullVersion;
        private string _normalizedVersion;

        // These properties are public only to facilitate testing.
        public NupkgMetadata NupkgMetadata { get; }
        public DateTime? CreatedDate { get; }
        public DateTime? LastEditedDate { get; }
        public DateTime? PublishedDate { get; }
        public PackageDeprecationItem Deprecation { get; }
        public IList<PackageVulnerabilityItem> Vulnerabilities { get; }

        public PackageCatalogItem(
            NupkgMetadata nupkgMetadata, 
            DateTime? createdDate = null, 
            DateTime? lastEditedDate = null, 
            DateTime? publishedDate = null, 
            string licenseNames = null, 
            string licenseReportUrl = null,
            PackageDeprecationItem deprecation = null,
            IList<PackageVulnerabilityItem> vulnerabilities = null)
        {
            NupkgMetadata = nupkgMetadata;
            CreatedDate = createdDate;
            LastEditedDate = lastEditedDate;
            PublishedDate = publishedDate;
            Deprecation = deprecation;
            Vulnerabilities = vulnerabilities;
        }

        public override IGraph CreateContentGraph(CatalogContext context)
        {
            IGraph graph = Utils.CreateNuspecGraph(NupkgMetadata.Nuspec, GetBaseAddress().ToString(), normalizeXml: true);

            // catalog infrastructure fields
            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode permanentType = graph.CreateUriNode(Schema.DataTypes.Permalink);
            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
            graph.Assert(resource.Subject, rdfTypePredicate, permanentType);

            // published
            INode publishedPredicate = graph.CreateUriNode(Schema.Predicates.Published);
            DateTime published = PublishedDate ?? TimeStamp;
            graph.Assert(resource.Subject, publishedPredicate, graph.CreateLiteralNode(published.ToString("O"), Schema.DataTypes.DateTime));

            // listed
            INode listedPredicate = graph.CreateUriNode(Schema.Predicates.Listed);
            Boolean listed = GetListed(published);
            graph.Assert(resource.Subject, listedPredicate, graph.CreateLiteralNode(listed.ToString(), Schema.DataTypes.Boolean));

            // created
            INode createdPredicate = graph.CreateUriNode(Schema.Predicates.Created);
            DateTime created = CreatedDate ?? TimeStamp;
            graph.Assert(resource.Subject, createdPredicate, graph.CreateLiteralNode(created.ToString("O"), Schema.DataTypes.DateTime));

            // lastEdited
            INode lastEditedPredicate = graph.CreateUriNode(Schema.Predicates.LastEdited);
            DateTime lastEdited = LastEditedDate ?? DateTime.MinValue;
            graph.Assert(resource.Subject, lastEditedPredicate, graph.CreateLiteralNode(lastEdited.ToString("O"), Schema.DataTypes.DateTime));

            // entries
            if (NupkgMetadata.Entries != null)
            {
                INode packageEntryPredicate = graph.CreateUriNode(Schema.Predicates.PackageEntry);
                INode packageEntryType = graph.CreateUriNode(Schema.DataTypes.PackageEntry);
                INode fullNamePredicate = graph.CreateUriNode(Schema.Predicates.FullName);
                INode namePredicate = graph.CreateUriNode(Schema.Predicates.Name);
                INode lengthPredicate = graph.CreateUriNode(Schema.Predicates.Length);
                INode compressedLengthPredicate = graph.CreateUriNode(Schema.Predicates.CompressedLength);

                foreach (PackageEntry entry in NupkgMetadata.Entries)
                {
                    Uri entryUri = new Uri(resource.Subject.ToString() + "#" + entry.FullName);

                    INode entryNode = graph.CreateUriNode(entryUri);

                    graph.Assert(resource.Subject, packageEntryPredicate, entryNode);
                    graph.Assert(entryNode, rdfTypePredicate, packageEntryType);
                    graph.Assert(entryNode, fullNamePredicate, graph.CreateLiteralNode(entry.FullName));
                    graph.Assert(entryNode, namePredicate, graph.CreateLiteralNode(entry.Name));
                    graph.Assert(entryNode, lengthPredicate, graph.CreateLiteralNode(entry.Length.ToString(), Schema.DataTypes.Integer));
                    graph.Assert(entryNode, compressedLengthPredicate, graph.CreateLiteralNode(entry.CompressedLength.ToString(), Schema.DataTypes.Integer));
                }
            }

            // packageSize and packageHash
            graph.Assert(resource.Subject, graph.CreateUriNode(Schema.Predicates.PackageSize), graph.CreateLiteralNode(NupkgMetadata.PackageSize.ToString(), Schema.DataTypes.Integer));
            graph.Assert(resource.Subject, graph.CreateUriNode(Schema.Predicates.PackageHash), graph.CreateLiteralNode(NupkgMetadata.PackageHash));
            graph.Assert(resource.Subject, graph.CreateUriNode(Schema.Predicates.PackageHashAlgorithm), graph.CreateLiteralNode(Constants.Sha512));

            // identity and version
            SetIdVersionFromGraph(graph);

            // deprecation
            if (Deprecation != null)
            {
                // assert deprecation root node to subject
                var deprecationPredicate = graph.CreateUriNode(Schema.Predicates.Deprecation);
                var deprecationRootNode = graph.CreateUriNode(new Uri(resource.Subject.ToString() + "#deprecation"));
                graph.Assert(resource.Subject, deprecationPredicate, deprecationRootNode);

                // assert reasons to deprecation root node
                var deprecationReasonRootNode = graph.CreateUriNode(Schema.Predicates.Reasons);
                foreach (var reason in Deprecation.Reasons)
                {
                    var reasonNode = graph.CreateLiteralNode(reason);
                    graph.Assert(deprecationRootNode, deprecationReasonRootNode, reasonNode);
                }

                // assert message to deprecation root node
                if (Deprecation.Message != null)
                {
                    graph.Assert(
                        deprecationRootNode,
                        graph.CreateUriNode(Schema.Predicates.Message),
                        graph.CreateLiteralNode(Deprecation.Message));
                }

                if (Deprecation.AlternatePackageId != null)
                {
                    // assert alternate package root node to deprecation root node
                    var deprecationAlternatePackagePredicate = graph.CreateUriNode(Schema.Predicates.AlternatePackage);
                    var deprecationAlternatePackageRootNode = graph.CreateUriNode(new Uri(resource.Subject.ToString() + "#deprecation/alternatePackage"));
                    graph.Assert(deprecationRootNode, deprecationAlternatePackagePredicate, deprecationAlternatePackageRootNode);

                    // assert id to alternate package root node
                    graph.Assert(
                        deprecationAlternatePackageRootNode,
                        graph.CreateUriNode(Schema.Predicates.Id),
                        graph.CreateLiteralNode(Deprecation.AlternatePackageId));

                    // assert version range to alternate package root node
                    graph.Assert(
                        deprecationAlternatePackageRootNode,
                        graph.CreateUriNode(Schema.Predicates.Range),
                        graph.CreateLiteralNode(Deprecation.AlternatePackageRange));
                }
            }

            // vulnerabilities
            if (Vulnerabilities != null)
            {
                INode vulnerabilityPredicate = graph.CreateUriNode(Schema.Predicates.Vulnerability);
                INode vulnerabilityType = graph.CreateUriNode(Schema.DataTypes.Vulnerability);
                INode advisoryUrlPredicate = graph.CreateUriNode(Schema.Predicates.AdvisoryUrl);
                INode severityPredicate = graph.CreateUriNode(Schema.Predicates.Severity);

                foreach (PackageVulnerabilityItem vulnerability in Vulnerabilities)
                {
                    Uri vulnerabilityUri = new Uri(resource.Subject.ToString() + "#vulnerability/GitHub/" + vulnerability.GitHubDatabaseKey);

                    INode vulnerabilityNode = graph.CreateUriNode(vulnerabilityUri);

                    graph.Assert(resource.Subject, vulnerabilityPredicate, vulnerabilityNode);
                    graph.Assert(vulnerabilityNode, rdfTypePredicate, vulnerabilityType);
                    graph.Assert(vulnerabilityNode, advisoryUrlPredicate, graph.CreateLiteralNode(vulnerability.AdvisoryUrl));
                    graph.Assert(vulnerabilityNode, severityPredicate, graph.CreateLiteralNode(vulnerability.Severity));
                }
            }

            return graph;
        }

        public static bool GetListed(DateTime published)
        {
            // if the published date is 1900/01/01, then the package is unlisted
            if (published.ToUniversalTime() == Constants.UnpublishedDate)
            {
                return false;
            }

            return true;
        }

        protected void SetIdVersionFromGraph(IGraph graph)
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
                _fullVersion = ((ILiteralNode)version.Object).Value;
                _normalizedVersion = NuGetVersionUtility.NormalizeVersion(_fullVersion);
            }
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            // metadata from nuspec

            using (IGraph graph = CreateContentGraph(context))
            {
                // catalog infrastructure fields
                INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
                INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp);
                INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);

                Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
                graph.Assert(resource.Subject, timeStampPredicate, graph.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
                graph.Assert(resource.Subject, commitIdPredicate, graph.CreateLiteralNode(CommitId.ToString()));

                if (graph.GetTriples(Schema.Predicates.Deprecation).Count() > 1)
                {
                    throw new ArgumentException("Package catalog items can only have a single deprecation.");
                }

                // create JSON content
                JObject frame = context.GetJsonLdContext("context.PackageDetails.json", GetItemType());

                StorageContent content = new StringStorageContent(Utils.CreateArrangedJson(graph, frame), "application/json", "no-store");

                return content;
            }
        }


        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDetails;
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

            if (_fullVersion != null)
            {
                graph.Assert(subject, versionPredicate, graph.CreateLiteralNode(_fullVersion));
            }

            return graph;
        }

        protected override string GetItemIdentity()
        {
            return (_id + "." + _normalizedVersion).ToLowerInvariant();
        }
    }
}