// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationMakerCatalogItemTests
    {
        [Theory]
        [InlineData("0001-01-01T00:00:00-01:00", true)]
        [InlineData("0001-01-01T00:00:00+00:00", true)]
        [InlineData("0001-01-01T00:00:00+01:00", true)]
        [InlineData("1900-01-01T00:00:00-01:00", false)]
        [InlineData("1900-01-01T00:00:00+00:00", false)]
        [InlineData("1900-01-01T00:00:00+01:00", true)] // Right at the end of 1899, UTC.
        [InlineData("1901-01-01T00:00:00-01:00", true)]
        [InlineData("1901-01-01T00:00:00+00:00", true)]
        [InlineData("1901-01-01T00:00:00+01:00", false)] // Right at the end of 1900, UTC.
        [InlineData("2015-01-01T00:00:00-01:00", true)]
        [InlineData("2015-01-01T00:00:00+00:00", true)]
        [InlineData("2015-01-01T00:00:00+01:00", true)]
        public void CreateContent_SetsListedProperly(string published, bool expectedListed)
        {
            // Arrange
            var catalogUri = new Uri("http://example/catalog/mypackage.1.0.0.json");
            var graph = new Graph();
            graph.Assert(
                graph.CreateUriNode(catalogUri),
                graph.CreateUriNode(Schema.Predicates.Version),
                graph.CreateLiteralNode("1.0.0"));
            graph.Assert(
                graph.CreateUriNode(catalogUri),
                graph.CreateUriNode(Schema.Predicates.Id),
                graph.CreateLiteralNode("MyPackage"));
            graph.Assert(
                graph.CreateUriNode(catalogUri),
                graph.CreateUriNode(Schema.Predicates.Published),
                graph.CreateLiteralNode(published));

            var registrationBaseAddress = new Uri("http://example/registration/");
            var packageContentBaseAddress = new Uri("http://example/content/");
            var item = new RegistrationMakerCatalogItem(
                catalogUri,
                graph,
                registrationBaseAddress,
                isExistingItem: false,
                postProcessGraph: g => g,
                packageContentBaseAddress: packageContentBaseAddress,
                forcePathProviderForIcons: false)
            {
                BaseAddress = new Uri("http://example/registration/mypackage/"),
            };
            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
            var context = new CatalogContext();

            // Act
            var content = item.CreateContent(context);

            // Assert
            var contentJson = JObject.Parse(GetContentString(content));
            var actualListed = contentJson["listed"].Value<bool>();
            Assert.Equal(expectedListed, actualListed);
        }

        private static string GetContentString(StorageContent content)
        {
            if (content is StringStorageContent stringStorageContent)
            {
                return stringStorageContent.Content;
            }

            using (var reader = new StreamReader(content.GetContentStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public class TheCreatePageContentMethod
        {
            private const string PackageId = "MyPackage";
            private const string PackageVersion = "01.02.03+ABC";
            private readonly Uri _catalogUri = new Uri("http://example/catalog/mypackage.1.0.0.json");
            private readonly Uri _registrationBaseAddress = new Uri("http://example/registration/");
            private readonly Uri _packageContentBaseAddress = new Uri("http://example/content/");
            private readonly Uri _baseAddress = new Uri("http://example/registration/mypackage/");
            private readonly Graph _graph;

            public TheCreatePageContentMethod()
            {
                _graph = new Graph();
                AddTriple(_catalogUri, Schema.Predicates.Id, PackageId);
                AddTriple(_catalogUri, Schema.Predicates.Version, PackageVersion);
                AddTriple(_catalogUri, Schema.Predicates.Published, "2015-01-01T00:00:00+00:00");
            }

            [Theory]
            [InlineData(null, null, null, "", "http://gallery.test")]
            [InlineData("https://test", null, null, "https://test", "http://gallery.test")]
            [InlineData("https://test", "TestExpression", null, "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData("https://test", null, "TestLicense", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, "TestExpression", null, "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "TestLicense", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "TestFile", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "TestLicense.txt", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "TestLicense.exe", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "TestLicense.any", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "/Folder/TestLicense", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test")]
            [InlineData(null, null, "TestLicense", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test/")]
            [InlineData(null, null, "TestLicense", "http://gallery.test/packages/MyPackage/1.2.3/license", "http://gallery.test//")]
            public void CreatePageContent_SetsLicenseUrlAndExpressionProperly(
                string licenseUrl,
                string licenseExpression,
                string licenseFile,
                string expectedLicenseUrlValue,
                string galleryBaseAddress)
            {
                // Arrange
                AddTriple(_catalogUri, Schema.Predicates.LicenseUrl, licenseUrl);
                AddTriple(_catalogUri, Schema.Predicates.LicenseExpression, licenseExpression);
                AddTriple(_catalogUri, Schema.Predicates.LicenseFile, licenseFile);

                var item = new RegistrationMakerCatalogItem(
                    _catalogUri,
                    _graph,
                    _registrationBaseAddress,
                    isExistingItem: false,
                    postProcessGraph: g => g,
                    packageContentBaseAddress: _packageContentBaseAddress,
                    galleryBaseAddress: new Uri(galleryBaseAddress),
                    forcePathProviderForIcons: false)
                {
                    BaseAddress = _baseAddress,
                };

                RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
                var context = new CatalogContext();

                // Act
                var content = item.CreatePageContent(context);
                var licenseUrlTriples = GetTriples(content, _catalogUri, Schema.Predicates.LicenseUrl);
                var licenseExpressionTriples = GetTriples(content, _catalogUri, Schema.Predicates.LicenseExpression);
                var licenseFileTriples = GetTriples(content, _catalogUri, Schema.Predicates.LicenseFile);

                // Assert
                Assert.Equal(expectedLicenseUrlValue, Assert.Single(licenseUrlTriples).Object.ToString());
                Assert.Equal(licenseExpression ?? "", Assert.Single(licenseExpressionTriples).Object.ToString());
                Assert.Empty(licenseFileTriples);
            }

            [Theory]
            [InlineData(null, null, false, "")]
            [InlineData("http://icon.test/", null, false, "http://icon.test/")]
            [InlineData("http://icon.test/", null, true, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.png", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.png", true, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData("http://icon.test/", "icon.png", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData("http://icon.test/", "icon.png", true, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.jpg", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.jpeg", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.gif", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.svg", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            [InlineData(null, "icon.exe", false, "http://example/content/test-container/mypackage/1.2.3/icon")]
            public void CreatePageContent_SetsIconUrlProperly(string iconUrl, string iconFile, bool forceFlatContainer, string expectedIconUrl)
            {
                // Arrange
                AddTriple(_catalogUri, Schema.Predicates.IconUrl, iconUrl);
                AddTriple(_catalogUri, Schema.Predicates.IconFile, iconFile);

                var item = new RegistrationMakerCatalogItem(
                    _catalogUri,
                    _graph,
                    _registrationBaseAddress,
                    isExistingItem: false,
                    postProcessGraph: g => g,
                    packageContentBaseAddress: _packageContentBaseAddress,
                    galleryBaseAddress: new Uri("http://gallery.test"),
                    forcePathProviderForIcons: forceFlatContainer)
                {
                    BaseAddress = _baseAddress,
                };
                RegistrationMakerCatalogItem.PackagePathProvider = new FlatContainerPackagePathProvider("test-container");
                var context = new CatalogContext();

                // Act
                var graph = item.CreatePageContent(context);

                // Assert
                var iconUrlTriples = GetTriples(graph, _catalogUri, Schema.Predicates.IconUrl);
                var iconFileTriples = GetTriples(graph, _catalogUri, Schema.Predicates.IconFile);

                Assert.Equal(expectedIconUrl, Assert.Single(iconUrlTriples).Object.ToString());
                Assert.Empty(iconFileTriples);
            }

            private void AddTriple(Uri subject, Uri predicate, string @object)
            {
                if (@object != null)
                {
                    _graph.Assert(
                        _graph.CreateUriNode(subject),
                        _graph.CreateUriNode(predicate),
                        _graph.CreateLiteralNode(@object));
                }
            }

            private IEnumerable<Triple> GetTriples(IGraph graph, Uri subject, Uri predicate)
            {
                return graph.GetTriplesWithSubjectPredicate(graph.CreateUriNode(subject), graph.CreateUriNode(predicate));
            }

            public static IEnumerable<object[]> CreatePageContent_SetsDeprecationInformationProperly_Data
            {
                get
                {
                    foreach (var shouldPostProcess in new[] { false, true })
                    {
                        foreach (var reason in
                            new[]
                            {
                            new[] { "first" },
                            new[] { "first", "second" }
                            })
                        {
                            foreach (var message in new[] { null, "this is the message" })
                            {
                                yield return new object[] { shouldPostProcess, reason, message, null, null };
                                yield return new object[] { shouldPostProcess, reason, message, "theId", "homeOnTheRange" };
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(CreatePageContent_SetsDeprecationInformationProperly_Data))]
            public void CreatePageContent_SetsDeprecationInformationProperly(
                bool shouldPostProcess,
                IEnumerable<string> reasons,
                string message,
                string alternatePackageId,
                string alternatePackageRange)
            {
                if (alternatePackageId == null && alternatePackageRange != null)
                {
                    throw new ArgumentException("Must specify alternate package range if alternate package ID is specified.");
                }

                if (alternatePackageId != null && alternatePackageRange == null)
                {
                    throw new ArgumentException("Must specify alternate package ID if alternate package range is specified.");
                }

                // Arrange
                var rootNode = _graph.CreateUriNode(_catalogUri);
                var deprecationPredicate = _graph.CreateUriNode(Schema.Predicates.Deprecation);
                var deprecationRootNode = _graph.CreateUriNode(new Uri(_catalogUri.ToString() + "#deprecation"));
                _graph.Assert(rootNode, deprecationPredicate, deprecationRootNode);

                var deprecationReasonRootNode = _graph.CreateUriNode(Schema.Predicates.Reasons);
                foreach (var reason in reasons)
                {
                    var reasonNode = _graph.CreateLiteralNode(reason);
                    _graph.Assert(deprecationRootNode, deprecationReasonRootNode, reasonNode);
                }

                if (message != null)
                {
                    _graph.Assert(
                        deprecationRootNode,
                        _graph.CreateUriNode(Schema.Predicates.Message),
                        _graph.CreateLiteralNode(message));
                }

                if (alternatePackageId != null)
                {
                    var deprecationAlternatePackagePredicate = _graph.CreateUriNode(Schema.Predicates.AlternatePackage);
                    var deprecationAlternatePackageRootNode = _graph.CreateUriNode(new Uri(_catalogUri.ToString() + "#deprecation/alternatePackage"));
                    _graph.Assert(deprecationRootNode, deprecationAlternatePackagePredicate, deprecationAlternatePackageRootNode);

                    _graph.Assert(
                        deprecationAlternatePackageRootNode,
                        _graph.CreateUriNode(Schema.Predicates.Id),
                        _graph.CreateLiteralNode(alternatePackageId));

                    _graph.Assert(
                        deprecationAlternatePackageRootNode,
                        _graph.CreateUriNode(Schema.Predicates.Range),
                        _graph.CreateLiteralNode(alternatePackageRange));
                }

                var item = new RegistrationMakerCatalogItem(
                    _catalogUri,
                    _graph,
                    _registrationBaseAddress,
                    isExistingItem: false,
                    postProcessGraph: shouldPostProcess ? RegistrationCollector.FilterOutDeprecationInformation : g => g,
                    packageContentBaseAddress: _packageContentBaseAddress,
                    forcePathProviderForIcons: false)
                {
                    BaseAddress = _baseAddress,
                };

                RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
                var context = new CatalogContext();

                // Act
                var content = item.CreatePageContent(context);

                // Assert
                var deprecationPredicateTriples = content
                    .GetTriplesWithSubjectPredicate(
                        content.CreateUriNode(_catalogUri),
                        content.CreateUriNode(Schema.Predicates.Deprecation));

                if (shouldPostProcess)
                {
                    Assert.Empty(deprecationPredicateTriples);
                    return;
                }

                var deprecationObjectNode = deprecationPredicateTriples
                    .Single()
                    .Object;

                var deprecationTriples = content.GetTriplesWithSubject(deprecationObjectNode);
                var reasonTriples = deprecationTriples
                    .Where(t => t.HasPredicate(content.CreateUriNode(Schema.Predicates.Reasons)));

                foreach (var reason in reasons)
                {
                    Assert.Contains(reasonTriples, t => t.HasObject(content.CreateLiteralNode(reason)));
                }

                if (message == null)
                {
                    Assert.DoesNotContain(
                        deprecationTriples, 
                        t => t.HasPredicate(content.CreateUriNode(Schema.Predicates.Message)));
                }
                else
                {
                    Assert.Contains(
                        deprecationTriples, 
                        t => t.HasPredicate(content.CreateUriNode(Schema.Predicates.Message)) && t.HasObject(content.CreateLiteralNode(message)));
                }

                if (alternatePackageId == null)
                {
                    Assert.DoesNotContain(
                        deprecationTriples,
                        t => t.HasPredicate(content.CreateUriNode(Schema.Predicates.AlternatePackage)));
                }
                else
                {
                    var alternatePackageObjectNode = content
                        .GetTriplesWithSubjectPredicate(
                            deprecationObjectNode,
                            content.CreateUriNode(Schema.Predicates.AlternatePackage))
                        .Single()
                        .Object;

                    var alternatePackageTriples = content.GetTriplesWithSubject(alternatePackageObjectNode);
                    Assert.Contains(alternatePackageTriples,
                        t => t.HasPredicate(content.CreateUriNode(Schema.Predicates.Id)) && t.HasObject(content.CreateLiteralNode(alternatePackageId)));

                    Assert.Contains(alternatePackageTriples,
                        t => t.HasPredicate(content.CreateUriNode(Schema.Predicates.Range)) && t.HasObject(content.CreateLiteralNode(alternatePackageRange)));
                }
            }
        }
    }
}