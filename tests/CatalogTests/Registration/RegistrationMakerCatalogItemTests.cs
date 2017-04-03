// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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
    }
}
