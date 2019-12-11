// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Moq;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Helpers
{
    public class PackageCatalogItemTests
    {
        [Theory]
        [InlineData("Newtonsoft.Json.9.0.2-beta1")]
        [InlineData("TestPackage.SemVer2.1.0.0-alpha.1")]
        [InlineData("DependencyMissingId.0.1.0")] // One dependency missing an ID attribute
        [InlineData("EmptyDependenciesElement.0.1.0")] // A <dependencies> element with no children
        [InlineData("EmptyDependencyId.0.1.0")] // One dependency with an empty string ID
        [InlineData("EmptyDependencyIdWithGroups.0.1.0")] // Using dependency groups, one dependency with an empty string ID
        [InlineData("OneValidDependencyOneEmptyId.0.1.0")] // One valid dependency and one with empty string ID
        [InlineData("OneValidDependencyOneEmptyIdWithGroups.0.1.0")] // Using dependency groups, one valid dependency and one with empty string ID
        [InlineData("WhitespaceDependencyId.0.1.0")] // One dependency with an ID only containing whitespace
        [InlineData("EmptyDependencyVersionRange.0.1.0")] // A dependency with a version range that is an empty string
        [InlineData("InvalidDependencyVersionRange.0.1.0")] //A dependency with a version range that is invalid
        [InlineData("MissingDependencyVersionRange.0.1.0")] // A dependency with no version range attribute
        [InlineData("WhitespaceDependencyVersionRange.0.1.0")] // A dependency with a version range that is whitespace
        [InlineData("PackageTypeCollapseDuplicate")]
        [InlineData("PackageTypeMultiple")]
        [InlineData("PackageTypeMultipleTypesNodes")]
        [InlineData("PackageTypeSingle")]
        [InlineData("PackageTypeSingleWithVersion")]
        [InlineData("PackageTypeSameTypeDifferentCase")]
        [InlineData("PackageTypeSameTypeDifferentVersionType")]
        [InlineData("PackageTypeSameTypeTwoVersion")]
        [InlineData("PackageTypeWhiteSpace")]
        [InlineData("PackageTypeWhiteSpaceVersion")]
        public void CreateContent_ProducesExpectedJson(string packageName)
        {
            // Arrange
            var catalogItem = CreateCatalogItem(packageName);
            var catalogContext = new CatalogContext();

            // Act
            var content = catalogItem.CreateContent(catalogContext);

            // Assert
            var expected = File.ReadAllText(Path.Combine("TestData", $"{packageName}.json"));
            string actual;
            using (var reader = new StreamReader(content.GetContentStream()))
            {
                actual = reader.ReadToEnd();
            }

            Assert.Equal("no-store", content.CacheControl);
            Assert.Equal("application/json", content.ContentType);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Newtonsoft.Json.9.0.2-beta1", "http://example/data/2017.01.04.08.15.00/newtonsoft.json.9.0.2-beta1.json")]
        [InlineData("TestPackage.SemVer2.1.0.0-alpha.1", "http://example/data/2017.01.04.08.15.00/testpackage.semver2.1.0.0-alpha.1.json")]
        public void CreateContent_HasExpectedItemAddress(string packageName, string expected)
        {
            // Arrange
            var catalogItem = CreateCatalogItem(packageName);
            var catalogContext = new CatalogContext();
            catalogItem.CreateContent(catalogContext);

            // Act
            var itemAddress = catalogItem.GetItemAddress();

            // Assert
            Assert.Equal(new Uri(expected), itemAddress);
        }

        [Theory]
        [InlineData("Newtonsoft.Json.9.0.2-beta1")]
        [InlineData("TestPackage.SemVer2.1.0.0-alpha.1")]
        public void CreateContent_HasExpectedItemType(string packageName)
        {
            // Arrange
            var catalogItem = CreateCatalogItem(packageName);
            var catalogContext = new CatalogContext();
            catalogItem.CreateContent(catalogContext);

            // Act
            var itemAddress = catalogItem.GetItemType();

            // Assert
            Assert.Equal(new Uri("http://schema.nuget.org/schema#PackageDetails"), itemAddress);
        }

        [Theory]
        [InlineData("Newtonsoft.Json.9.0.2-beta1", "Newtonsoft.Json", "9.0.2-beta1")]
        [InlineData("TestPackage.SemVer2.1.0.0-alpha.1", "TestPackage.SemVer2", "1.0.0-alpha.1+githash")]
        public void CreateContent_HasExpectedPageContent(string packageName, string id, string version)
        {
            // Arrange
            var catalogItem = CreateCatalogItem(packageName);
            var catalogContext = new CatalogContext();
            catalogItem.CreateContent(catalogContext);

            // Act
            var pageContent = catalogItem.CreatePageContent(catalogContext);

            // Assert
            var triples = pageContent
                .Triples
                .Cast<Triple>()
                .OrderBy(x => x.Subject)
                .ThenBy(x => x.Predicate)
                .ToList();
            Assert.Equal(2, triples.Count);
            Assert.Equal(Schema.Predicates.Id.ToString(), triples[0].Predicate.ToString());
            Assert.Equal(id, triples[0].Object.ToString());
            Assert.Equal(Schema.Predicates.Version.ToString(), triples[1].Predicate.ToString());
            Assert.Equal(version, triples[1].Object.ToString());
        }

        [Fact]
        public void CreateContent_ThrowsIfMultipleDeprecationTriples()
        {
            var packageDetails = Schema.DataTypes.PackageDetails;
            var catalogItemMock = new Mock<PackageCatalogItem>(null, null, null, null, null, null, null)
            {
                CallBase = true
            };

            var context = new CatalogContext();

            catalogItemMock
                .Setup(x => x.GetItemType())
                .Returns(packageDetails);

            var graph = new Graph();
            var subject = graph.CreateBlankNode();
            graph.Assert(
                subject, 
                graph.CreateUriNode(Schema.Predicates.Type), 
                graph.CreateUriNode(packageDetails));

            graph.Assert(
                subject,
                graph.CreateUriNode(Schema.Predicates.Deprecation),
                graph.CreateLiteralNode("deprecation1"));

            graph.Assert(
                subject,
                graph.CreateUriNode(Schema.Predicates.Deprecation),
                graph.CreateLiteralNode("deprecation2"));

            catalogItemMock
                .Setup(x => x.CreateContentGraph(context))
                .Returns(graph);

            Assert.Throws<ArgumentException>(
                () => catalogItemMock.Object.CreateContent(context));
        }

        private static CatalogItem CreateCatalogItem(string packageName)
        {
            var path = Path.GetFullPath(Path.Combine("TestData", $"{packageName}.nupkg"));

            using (var packageStream = TestHelper.GetStream($"{packageName}.nupkg"))
            {
                var createdDate = new DateTime(2017, 1, 1, 8, 15, 0, DateTimeKind.Utc);
                var lastEditedDate = new DateTime(2017, 1, 2, 8, 15, 0, DateTimeKind.Utc);
                var publishedDate = new DateTime(2017, 1, 3, 8, 15, 0, DateTimeKind.Utc);

                var baseAddress = new Uri("http://example/catalog");
                var timestamp = new DateTime(2017, 1, 4, 8, 15, 0, DateTimeKind.Utc);
                var commitId = new Guid("4AEE0EF4-A039-4460-BD5F-98F944E33289");
                                
                var catalogItem = Utils.CreateCatalogItem(
                    path,
                    packageStream,
                    createdDate,
                    lastEditedDate,
                    publishedDate);
                catalogItem.TimeStamp = timestamp;
                catalogItem.CommitId = commitId;
                catalogItem.BaseAddress = baseAddress;

                return catalogItem;
            }
        }
    }
}
