// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
                packageContentBaseAddress: packageContentBaseAddress)
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
            var stringStorageContent = content as StringStorageContent;
            if (stringStorageContent != null)
            {
                return stringStorageContent.Content;
            }

            using (var reader = new StreamReader(content.GetContentStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public class CreatePageContent_SetsLicenseUrlAndExpression
        {
            private readonly Uri _catalogUri = new Uri("http://example/catalog/mypackage.1.0.0.json");
            private readonly Uri _registrationBaseAddress = new Uri("http://example/registration/");
            private readonly Uri _packageContentBaseAddress = new Uri("http://example/content/");
            private readonly Uri _baseAddress = new Uri("http://example/registration/mypackage/");
            private Graph _graph;

            public CreatePageContent_SetsLicenseUrlAndExpression()
            {
                _graph = new Graph();
                _graph.Assert(
                    _graph.CreateUriNode(_catalogUri),
                    _graph.CreateUriNode(Schema.Predicates.Id),
                    _graph.CreateLiteralNode("MyPackage"));
                _graph.Assert(
                    _graph.CreateUriNode(_catalogUri),
                    _graph.CreateUriNode(Schema.Predicates.Version),
                    _graph.CreateLiteralNode("01.02.03+ABC"));
                _graph.Assert(
                    _graph.CreateUriNode(_catalogUri),
                    _graph.CreateUriNode(Schema.Predicates.Published),
                    _graph.CreateLiteralNode("2015-01-01T00:00:00+00:00"));
            }

            [Theory]
            [InlineData(false, false, false, "", "", "http://gallery.org")]
            [InlineData(true, false, false, "https://test.org", "", "http://gallery.org")]
            [InlineData(true, true, false, "http://gallery.org/packages/MyPackage/1.2.3/license", "MIT", "http://gallery.org")]
            [InlineData(true, false, true, "http://gallery.org/packages/MyPackage/1.2.3/license", "", "http://gallery.org")]
            [InlineData(false, true, false, "http://gallery.org/packages/MyPackage/1.2.3/license", "MIT", "http://gallery.org")]
            [InlineData(false, false, true, "http://gallery.org/packages/MyPackage/1.2.3/license", "", "http://gallery.org")]
            [InlineData(false, false, true, "http://gallery.org/packages/MyPackage/1.2.3/license", "", "http://gallery.org/")]
            [InlineData(false, false, true, "http://gallery.org/packages/MyPackage/1.2.3/license", "", "http://gallery.org//")]
            public void CreatePageContent_SetsLicenseUrlAndExpressionProperly(bool hasLicenseUrl, bool hasLicenseExpression, bool hasLicenseFile,
                                                                             string expectedLicenseUrlValue, string expectedLicenseExpressionValue, string galleryBaseAddress)
            {
                // Arrange
                if (hasLicenseUrl)
                {
                    _graph.Assert(
                    _graph.CreateUriNode(_catalogUri),
                    _graph.CreateUriNode(Schema.Predicates.LicenseUrl),
                    _graph.CreateLiteralNode("https://test.org"));
                }
                if (hasLicenseExpression)
                {
                    _graph.Assert(
                    _graph.CreateUriNode(_catalogUri),
                    _graph.CreateUriNode(Schema.Predicates.LicenseExpression),
                    _graph.CreateLiteralNode("MIT"));
                }
                if (hasLicenseFile)
                {
                    _graph.Assert(
                    _graph.CreateUriNode(_catalogUri),
                    _graph.CreateUriNode(Schema.Predicates.LicenseFile),
                    _graph.CreateLiteralNode("license"));
                }

                var item = new RegistrationMakerCatalogItem(
                    _catalogUri,
                    _graph,
                    _registrationBaseAddress,
                    isExistingItem: false,
                    packageContentBaseAddress: _packageContentBaseAddress,
                    galleryBaseAddress: new Uri(galleryBaseAddress))
                {
                    BaseAddress = _baseAddress,
                };
                RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
                var context = new CatalogContext();

                // Act
                var content = item.CreatePageContent(context);
                var licenseUrlTriples = content.GetTriplesWithSubjectPredicate(
                    content.CreateUriNode(_catalogUri),
                    content.CreateUriNode(Schema.Predicates.LicenseUrl));
                var licenseExpressionTriples = content.GetTriplesWithSubjectPredicate(
                    content.CreateUriNode(_catalogUri),
                    content.CreateUriNode(Schema.Predicates.LicenseExpression));
                var licenseFileTriples = content.GetTriplesWithSubjectPredicate(
                    content.CreateUriNode(_catalogUri),
                    content.CreateUriNode(Schema.Predicates.LicenseFile));

                // Assert
                Assert.Equal(1, licenseUrlTriples.Count());
                Assert.Equal(expectedLicenseUrlValue, licenseUrlTriples.First().Object.ToString());
                Assert.Equal(1, licenseExpressionTriples.Count());
                Assert.Equal(expectedLicenseExpressionValue, licenseExpressionTriples.First().Object.ToString());
                Assert.Equal(0, licenseFileTriples.Count());
            }
        }
    }
}