// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Extensions
{
    public class DotNetRdfExtensionsTests
    {
        // Covers copying V3 catalog URIs when CatalogWriterBase.LoadIndexResource extracts item content.
        // For example, package leaf subjects such as https://api.nuget.org/v3/catalog0/data/.../package.1.0.0.json
        // and predicates such as nuget:id must remain assertable in the extracted itemContent graph.
        [Fact]
        public void CopyNode_WithUriNode_CopiesUriToTargetGraph()
        {
            var source = new Graph();
            var target = new Graph();
            var uri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json");
            var node = source.CreateUriNode(uri);

            var copied = node.CopyNode(target);

            var copiedUriNode = Assert.IsAssignableFrom<IUriNode>(copied);
            Assert.Equal(uri, copiedUriNode.Uri);

            var predicate = target.CreateUriNode(Schema.Predicates.Id);
            var obj = target.CreateLiteralNode("Newtonsoft.Json");
            target.Assert(copied, predicate, obj);
            Assert.Single(target.GetTriplesWithSubjectPredicate(copied, predicate));
        }

        // Covers copying string-valued V3 catalog metadata while building itemContent graphs.
        // For example, nuget:id, nuget:version, authors, descriptions, iconFile, licenseExpression,
        // and readmeFile values must survive graph extraction and later serialization unchanged.
        [Theory]
        [InlineData("id", "Newtonsoft.Json")]
        [InlineData("version", "13.0.3")]
        [InlineData("authors", "James Newton-King")]
        [InlineData("description", "Json.NET is a popular high-performance JSON framework for .NET")]
        [InlineData("iconFile", "icon.png")]
        [InlineData("licenseExpression", "MIT")]
        [InlineData("readmeFile", "README.md")]
        public void CopyNode_WithPlainLiteralNode_CopiesV3StringMetadataValueToTargetGraph(
            string predicateName,
            string value)
        {
            var source = new Graph();
            var target = new Graph();
            var subject = target.CreateUriNode(new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json"));
            var predicate = target.CreateUriNode(new Uri(Schema.Prefixes.NuGet + predicateName));
            var node = source.CreateLiteralNode(value);

            var copied = node.CopyNode(target);
            target.Assert(subject, predicate, copied);

            var copiedLiteralNode = Assert.IsAssignableFrom<ILiteralNode>(copied);
            Assert.Equal(value, copiedLiteralNode.Value);
            Assert.Equal(string.Empty, copiedLiteralNode.Language);
            Assert.Equal(node.DataType, copiedLiteralNode.DataType);
            Assert.Single(target.GetTriplesWithSubjectPredicate(subject, predicate));
        }

        // Covers copying typed V3 catalog literals used by catalog indexes and pages.
        // For example, catalog:commitTimeStamp, catalog:count, package size, and other JSON-LD scalar
        // values must keep their XML schema datatype when moved between RDF graphs.
        [Fact]
        public void CopyNode_WithTypedLiteralNode_CopiesValueAndDataTypeToTargetGraph()
        {
            var source = new Graph();
            var target = new Graph();
            var dataType = Schema.DataTypes.DateTime;
            var node = source.CreateLiteralNode("2026-03-27T18:45:05.7443991Z", dataType);

            var copied = node.CopyNode(target);

            var copiedLiteralNode = Assert.IsAssignableFrom<ILiteralNode>(copied);
            Assert.Equal("2026-03-27T18:45:05.7443991Z", copiedLiteralNode.Value);
            Assert.Equal(string.Empty, copiedLiteralNode.Language);
            Assert.Equal(dataType, copiedLiteralNode.DataType);
        }

        // Covers preserving language-tagged literals if V3 catalog JSON-LD content ever includes localized text.
        // For example, a localized description copied into itemContent must keep its language tag so RDF
        // consumers can distinguish it from the invariant package metadata value.
        [Fact]
        public void CopyNode_WithLanguageLiteralNode_CopiesValueAndLanguageToTargetGraph()
        {
            var source = new Graph();
            var target = new Graph();
            var node = source.CreateLiteralNode("Json.NET is a popular high-performance JSON framework for .NET", "en");

            var copied = node.CopyNode(target);

            var copiedLiteralNode = Assert.IsAssignableFrom<ILiteralNode>(copied);
            Assert.Equal("Json.NET is a popular high-performance JSON framework for .NET", copiedLiteralNode.Value);
            Assert.Equal("en", copiedLiteralNode.Language);
            Assert.Equal(node.DataType, copiedLiteralNode.DataType);
        }

        // Covers copying V3 catalog JSON-LD blank-node structures reached during recursive content copying.
        // For example, nested dependency groups, package types, framework assemblies, or deprecation metadata
        // represented as blank nodes must remain connected after being copied into the target graph.
        [Fact]
        public void CopyNode_WithBlankNode_CopiesInternalIdToTargetGraph()
        {
            var source = new Graph();
            var target = new Graph();
            var node = source.CreateBlankNode("dependency-group-netstandard2.0");

            var copied = node.CopyNode(target);

            var copiedBlankNode = Assert.IsAssignableFrom<IBlankNode>(copied);
            Assert.Equal("dependency-group-netstandard2.0", copiedBlankNode.InternalID);
        }

        // Covers copying complete V3 catalog RDF statements in Utils.CopyCatalogContentGraph.
        // For example, when an item links to another URI node, the recursive copy must move the whole
        // subject-predicate-object statement into itemContent before continuing traversal.
        [Fact]
        public void CopyTriple_CopiesAllNodesToTargetGraph()
        {
            var source = new Graph();
            var target = new Graph();
            var subject = source.CreateUriNode(new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json"));
            var predicate = source.CreateUriNode(Schema.Predicates.Version);
            var obj = source.CreateLiteralNode("13.0.3");
            var triple = new Triple(subject, predicate, obj);

            var copied = triple.CopyTriple(target);

            var copiedSubject = Assert.IsAssignableFrom<IUriNode>(copied.Subject);
            var copiedPredicate = Assert.IsAssignableFrom<IUriNode>(copied.Predicate);
            var copiedObject = Assert.IsAssignableFrom<ILiteralNode>(copied.Object);
            Assert.Equal(((IUriNode)subject).Uri, copiedSubject.Uri);
            Assert.Equal(((IUriNode)predicate).Uri, copiedPredicate.Uri);
            Assert.Equal(((ILiteralNode)obj).Value, copiedObject.Value);
            Assert.True(target.Assert(copied));
            Assert.Single(target.Triples);
        }

        // Covers the dotNetRDF 1.x compatibility overload used by CatalogWriterBase.LoadIndexResource.
        // For example, index item content is copied with keepOriginalGraphUri: false, matching the old
        // library behavior needed by V3 catalog index loading without preserving source graph URIs.
        [Fact]
        public void CopyNode_WithKeepOriginalGraphUriFalse_MatchesCopyNodeOverloadWithoutFlag()
        {
            var source = new Graph();
            var target = new Graph();
            var node = source.CreateLiteralNode("13.0.3");

            var copiedWithFlag = node.CopyNode(target, keepOriginalGraphUri: false);
            var copiedWithoutFlag = node.CopyNode(target);

            Assert.Equal(copiedWithoutFlag, copiedWithFlag);
        }

        // Covers loading a realistic V3 catalog page/index shape and extracting package item content.
        // For example, the page has catalog:item links and index bookkeeping, while the package details
        // itemContent graph keeps only package metadata and recursively copied package entry statements.
        [Fact]
        public async Task LoadIndexResource_WithV3CatalogPage_CopiesItemContentGraph()
        {
            var pageUri = new Uri("https://api.nuget.org/v3/catalog0/page22290.json");
            var leafUri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json");
            var packageEntryUri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json#packageEntry");
            var commitId = new Guid("8f58aef4-70e0-4385-a48e-b4bdbbc16018");
            var commitTimeStamp = DateTime.Parse("2026-03-27T18:45:05.7443991Z").ToUniversalTime();
            var graph = new Graph();
            var pageNode = graph.CreateUriNode(pageUri);
            var leafNode = graph.CreateUriNode(leafUri);
            var packageEntryNode = graph.CreateUriNode(packageEntryUri);

            graph.Assert(pageNode, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.CatalogPage));
            graph.Assert(pageNode, graph.CreateUriNode(Schema.Predicates.CatalogCommitId), graph.CreateLiteralNode(commitId.ToString()));
            graph.Assert(pageNode, graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp), graph.CreateLiteralNode(commitTimeStamp.ToString("O"), Schema.DataTypes.DateTime));
            graph.Assert(pageNode, graph.CreateUriNode(Schema.Predicates.CatalogCount), graph.CreateLiteralNode("1", Schema.DataTypes.Integer));
            graph.Assert(pageNode, graph.CreateUriNode(Schema.Predicates.CatalogItem), leafNode);
            graph.Assert(leafNode, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.PackageDetails));
            graph.Assert(leafNode, graph.CreateUriNode(Schema.Predicates.CatalogCommitId), graph.CreateLiteralNode(commitId.ToString()));
            graph.Assert(leafNode, graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp), graph.CreateLiteralNode(commitTimeStamp.ToString("O"), Schema.DataTypes.DateTime));
            graph.Assert(leafNode, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode("Newtonsoft.Json"));
            graph.Assert(leafNode, graph.CreateUriNode(Schema.Predicates.Version), graph.CreateLiteralNode("13.0.3"));
            graph.Assert(leafNode, graph.CreateUriNode(Schema.Predicates.PackageEntry), packageEntryNode);
            graph.Assert(packageEntryNode, graph.CreateUriNode(Schema.Predicates.FullName), graph.CreateLiteralNode("lib/netstandard2.0/Newtonsoft.Json.dll"));
            graph.Assert(packageEntryNode, graph.CreateUriNode(Schema.Predicates.Length), graph.CreateLiteralNode("701992", Schema.DataTypes.Integer));

            var writer = CreateWriter(pageUri, graph);

            var entries = await writer.LoadIndexResourceAsync(pageUri);

            var entry = Assert.Single(entries);
            Assert.Equal(leafUri.AbsoluteUri, entry.Key);
            Assert.Equal(Schema.DataTypes.PackageDetails, entry.Value.Type);
            Assert.Equal(commitId, entry.Value.CommitId);
            Assert.Equal(commitTimeStamp, entry.Value.CommitTimeStamp);
            Assert.Null(entry.Value.Count);
            Assert.NotNull(entry.Value.Content);
            AssertLiteral(entry.Value.Content, leafUri, Schema.Predicates.Id, "Newtonsoft.Json");
            AssertLiteral(entry.Value.Content, leafUri, Schema.Predicates.Version, "13.0.3");
            AssertLiteral(entry.Value.Content, packageEntryUri, Schema.Predicates.FullName, "lib/netstandard2.0/Newtonsoft.Json.dll");
            AssertLiteral(entry.Value.Content, packageEntryUri, Schema.Predicates.Length, "701992");
            Assert.Empty(entry.Value.Content.GetTriplesWithSubjectPredicate(
                entry.Value.Content.CreateUriNode(leafUri),
                entry.Value.Content.CreateUriNode(Schema.Predicates.CatalogCommitId)));
        }

        // Covers recursively copying the package-details subgraph shape used by V3 catalog leaves.
        // For example, package entry and deprecation nodes linked from the package leaf must be copied,
        // but catalog page/root nodes reached through catalog:parent are recognized as catalog structure.
        [Fact]
        public void CopyCatalogContentGraph_WithV3PackageDetailsSubgraph_CopiesRecursiveMetadataGraph()
        {
            var source = new Graph();
            var target = new Graph();
            var leafUri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json");
            var packageEntryUri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json#packageEntry");
            var deprecationUri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json#deprecation");
            var alternatePackageUri = new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json#alternatePackage");
            var pageUri = new Uri("https://api.nuget.org/v3/catalog0/page22290.json");
            var leafNode = source.CreateUriNode(leafUri);
            var packageEntryNode = source.CreateUriNode(packageEntryUri);
            var deprecationNode = source.CreateUriNode(deprecationUri);
            var alternatePackageNode = source.CreateUriNode(alternatePackageUri);
            var pageNode = source.CreateUriNode(pageUri);

            source.Assert(leafNode, source.CreateUriNode(Schema.Predicates.Type), source.CreateUriNode(Schema.DataTypes.PackageDetails));
            source.Assert(leafNode, source.CreateUriNode(Schema.Predicates.Id), source.CreateLiteralNode("Newtonsoft.Json"));
            source.Assert(leafNode, source.CreateUriNode(Schema.Predicates.Version), source.CreateLiteralNode("13.0.3"));
            source.Assert(leafNode, source.CreateUriNode(Schema.Predicates.PackageEntry), packageEntryNode);
            source.Assert(packageEntryNode, source.CreateUriNode(Schema.Predicates.FullName), source.CreateLiteralNode("lib/netstandard2.0/Newtonsoft.Json.dll"));
            source.Assert(packageEntryNode, source.CreateUriNode(Schema.Predicates.Length), source.CreateLiteralNode("701992", Schema.DataTypes.Integer));
            source.Assert(leafNode, source.CreateUriNode(Schema.Predicates.Deprecation), deprecationNode);
            source.Assert(deprecationNode, source.CreateUriNode(Schema.Predicates.Reasons), source.CreateLiteralNode("Legacy"));
            source.Assert(deprecationNode, source.CreateUriNode(Schema.Predicates.Message), source.CreateLiteralNode("Use System.Text.Json when possible."));
            source.Assert(deprecationNode, source.CreateUriNode(Schema.Predicates.AlternatePackage), alternatePackageNode);
            source.Assert(alternatePackageNode, source.CreateUriNode(Schema.Predicates.Id), source.CreateLiteralNode("System.Text.Json"));
            source.Assert(alternatePackageNode, source.CreateUriNode(Schema.Predicates.Range), source.CreateLiteralNode("[8.0.0, )"));
            source.Assert(leafNode, source.CreateUriNode(Schema.Predicates.CatalogParent), pageNode);
            source.Assert(pageNode, source.CreateUriNode(Schema.Predicates.Type), source.CreateUriNode(Schema.DataTypes.CatalogPage));
            source.Assert(pageNode, source.CreateUriNode(Schema.Predicates.CatalogCount), source.CreateLiteralNode("1", Schema.DataTypes.Integer));

            Utils.CopyCatalogContentGraph(leafNode, source, target);

            AssertLiteral(target, leafUri, Schema.Predicates.Id, "Newtonsoft.Json");
            AssertLiteral(target, leafUri, Schema.Predicates.Version, "13.0.3");
            AssertLiteral(target, packageEntryUri, Schema.Predicates.FullName, "lib/netstandard2.0/Newtonsoft.Json.dll");
            AssertLiteral(target, packageEntryUri, Schema.Predicates.Length, "701992");
            AssertLiteral(target, deprecationUri, Schema.Predicates.Reasons, "Legacy");
            AssertLiteral(target, deprecationUri, Schema.Predicates.Message, "Use System.Text.Json when possible.");
            AssertLiteral(target, alternatePackageUri, Schema.Predicates.Id, "System.Text.Json");
            AssertLiteral(target, alternatePackageUri, Schema.Predicates.Range, "[8.0.0, )");
            Assert.Single(target.GetTriplesWithSubjectPredicate(
                target.CreateUriNode(leafUri),
                target.CreateUriNode(Schema.Predicates.CatalogParent)));
            Assert.Empty(target.GetTriplesWithSubjectPredicate(
                target.CreateUriNode(pageUri),
                target.CreateUriNode(Schema.Predicates.CatalogCount)));
        }

        // Covers failing fast if V3 catalog item-content extraction receives malformed RDF with a missing node.
        // For example, CatalogWriterBase.LoadIndexResource should surface the problem instead of silently
        // producing incomplete itemContent for a catalog page entry.
        [Fact]
        public void CopyNode_WhenNodeIsNull_Throws()
        {
            var target = new Graph();

            var exception = Assert.Throws<ArgumentNullException>(() => ((INode)null).CopyNode(target));

            Assert.Equal("node", exception.ParamName);
        }

        // Covers failing fast if V3 catalog graph merging or item-content extraction is missing its target graph.
        // For example, Utils.RemoveExistingProperties and CatalogWriterBase.LoadIndexResource rely on the
        // target graph to compare, retract, and assert copied package metadata.
        [Fact]
        public void CopyNode_WhenTargetIsNull_Throws()
        {
            var source = new Graph();
            var node = source.CreateLiteralNode("Newtonsoft.Json");

            var exception = Assert.Throws<ArgumentNullException>(() => node.CopyNode(target: null));

            Assert.Equal("target", exception.ParamName);
        }

        // Covers failing fast if V3 catalog content recursion attempts to copy a missing RDF statement.
        // For example, Utils.CopyCatalogContentGraph should not hide corruption when recursively copying
        // statements reachable from a package details leaf into itemContent.
        [Fact]
        public void CopyTriple_WhenTripleIsNull_Throws()
        {
            var target = new Graph();

            var exception = Assert.Throws<ArgumentNullException>(() => ((Triple)null).CopyTriple(target));

            Assert.Equal("triple", exception.ParamName);
        }

        // Covers failing fast if V3 catalog content recursion is missing the graph receiving copied statements.
        // For example, recursive copying of dependency, framework, or deprecation statements must have a
        // target itemContent graph so the copied catalog leaf remains complete.
        [Fact]
        public void CopyTriple_WhenTargetIsNull_Throws()
        {
            var source = new Graph();
            var subject = source.CreateUriNode(new Uri("https://api.nuget.org/v3/catalog0/data/2026.03.27.18.45.05/newtonsoft.json.13.0.3.json"));
            var predicate = source.CreateUriNode(Schema.Predicates.Id);
            var obj = source.CreateLiteralNode("Newtonsoft.Json");
            var triple = new Triple(subject, predicate, obj);

            var exception = Assert.Throws<ArgumentNullException>(() => triple.CopyTriple(target: null));

            Assert.Equal("target", exception.ParamName);
        }

        private static TestableCatalogWriter CreateWriter(Uri resourceUri, IGraph graph)
        {
            var storage = new Mock<IStorage>(MockBehavior.Strict);
            storage.Setup(x => x.ResolveUri("index.json"))
                .Returns(new Uri("https://api.nuget.org/v3/catalog0/index.json"));
            storage.Setup(x => x.LoadStringAsync(resourceUri, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Utils.CreateJson(graph).ToString(Newtonsoft.Json.Formatting.None));

            return new TestableCatalogWriter(storage.Object);
        }

        private static void AssertLiteral(IGraph graph, Uri subjectUri, Uri predicateUri, string expectedValue)
        {
            var triples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(subjectUri),
                graph.CreateUriNode(predicateUri)).ToList();

            var triple = Assert.Single(triples);
            var literal = Assert.IsAssignableFrom<ILiteralNode>(triple.Object);
            Assert.Equal(expectedValue, literal.Value);
        }

        private sealed class TestableCatalogWriter : CatalogWriterBase
        {
            public TestableCatalogWriter(IStorage storage)
                : base(storage, Mock.Of<ITelemetryService>())
            {
            }

            public Task<IDictionary<string, CatalogItemSummary>> LoadIndexResourceAsync(Uri resourceUri)
            {
                return LoadIndexResource(resourceUri, CancellationToken.None);
            }

            protected override Task<SavePagesResult> SavePages(
                Guid commitId,
                DateTime commitTimeStamp,
                IDictionary<string, CatalogItemSummary> itemEntries,
                CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
