// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Services.Metadata.Catalog;
using Xunit;
using VDS.RDF;

namespace CatalogTests.Helpers
{
    public class UtilsTests
    {
        [Fact]
        public void GetNupkgMetadata_WhenStreamIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => Utils.GetNupkgMetadata(stream: null, packageHash: "a"));

            Assert.Equal("stream", exception.ParamName);
        }

        [Fact]
        public void GetNupkgMetadata_WhenPackageHashIsNull_GeneratesPackageHash()
        {
            using (var stream = GetPackageStream())
            {
                var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);

                Assert.NotNull(metadata.Nuspec);
                Assert.Equal(18, metadata.Entries.Count());
                Assert.Equal("bq5DjCtCJpy9R5rsEeQlKz8qGF1Bh3wGaJKMlRwmCoKZ8WUCIFtU3JlyMOdAkSn66KCehCCAxMZFOQD4nNnH/w==", metadata.PackageHash);
                Assert.Equal(1871318, metadata.PackageSize);
            }
        }

        [Fact]
        public void GetNupkgMetadata_WhenPackageHashIsProvided_UsesProvidePackageHash()
        {
            using (var stream = GetPackageStream())
            {
                var metadata = Utils.GetNupkgMetadata(stream, packageHash: "a");

                Assert.NotNull(metadata.Nuspec);
                Assert.Equal(18, metadata.Entries.Count());
                Assert.Equal("a", metadata.PackageHash);
                Assert.Equal(1871318, metadata.PackageSize);
            }
        }

        [Fact]
        public void GetNupkgMetadata_WhenNuspecNotFound_Throws()
        {
            using (var stream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zipArchive.CreateEntry("a");
                }

                stream.Position = 0;

                var exception = Assert.Throws<InvalidDataException>(
                      () => Utils.GetNupkgMetadata(stream, packageHash: null));

                Assert.StartsWith("Unable to find nuspec", exception.Message);
            }
        }

        [Fact]
        public void GetNupkgMetadata_WhenNuspecAtRootLevel_Succeeds()
        {
            using (var stream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = zipArchive.CreateEntry("package.nuspec");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write("<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\"><metadata><id>TestPackage</id><version>1.0.0</version><authors>Test</authors><description>Test</description></metadata></package>");
                    }
                }

                stream.Position = 0;

                var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);

                Assert.NotNull(metadata);
                Assert.NotNull(metadata.Nuspec);
            }
        }

        [Fact]
        public void GetNupkgMetadata_WhenNuspecInSubdirectoryWithForwardSlash_Throws()
        {
            using (var stream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = zipArchive.CreateEntry("subdir/package.nuspec");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write("<?xml version=\"1.0\"?><package></package>");
                    }
                }

                stream.Position = 0;

                var exception = Assert.Throws<InvalidDataException>(
                      () => Utils.GetNupkgMetadata(stream, packageHash: null));

                Assert.StartsWith("Unable to find nuspec", exception.Message);
            }
        }

        [Fact]
        public void GetNupkgMetadata_WhenNuspecInSubdirectoryWithBackslash_Throws()
        {
            using (var stream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = zipArchive.CreateEntry("subdir\\package.nuspec");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write("<?xml version=\"1.0\"?><package></package>");
                    }
                }

                stream.Position = 0;

                var exception = Assert.Throws<InvalidDataException>(
                      () => Utils.GetNupkgMetadata(stream, packageHash: null));

                Assert.StartsWith("Unable to find nuspec", exception.Message);
            }
        }


        [Fact]
        public void GetNupkgMetadata_WhenNuspecAtRootAndInSubdirectory_UsesRootNuspec()
        {
            using (var stream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var subdirEntry = zipArchive.CreateEntry("subdir\\malicious.nuspec");
                    using (var writer = new StreamWriter(subdirEntry.Open()))
                    {
                        writer.Write("<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\"><metadata><id>MaliciousPackage</id><version>2.0.0</version><authors>MaliciousAuthor</authors><description>Malicious Description</description></metadata></package>");
                    }

                    var rootEntry = zipArchive.CreateEntry("package.nuspec");
                    using (var writer = new StreamWriter(rootEntry.Open()))
                    {
                        writer.Write("<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\"><metadata><id>RootPackage</id><version>1.0.0</version><authors>RootAuthor</authors><description>Root Description</description></metadata></package>");
                    }
                }

                stream.Position = 0;

                var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);

                Assert.NotNull(metadata);
                Assert.NotNull(metadata.Nuspec);
                var packageId = metadata.Nuspec.Descendants().FirstOrDefault(x => x.Name.LocalName == "id");
                Assert.NotNull(packageId);
                Assert.Equal("RootPackage", packageId.Value);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetResourceStream_WhenResourceNameIsNullOrEmpty_Throws(string resourceName)
        {
            var exception = Assert.Throws<ArgumentException>(() => Utils.GetResourceStream(resourceName));

            Assert.Equal("resourceName", exception.ParamName);
        }

        [Fact]
        public void GetResourceStream_WhenResourceNameIsValid_ReturnsStream()
        {
            using (var stream = Utils.GetResourceStream("context.Catalog.json"))
            {
                Assert.NotNull(stream);
            }
        }

        private static MemoryStream GetPackageStream()
        {
            return TestHelper.GetStream("Newtonsoft.Json.9.0.2-beta1.nupkg.testdata");
        }

        [Fact]
        public void GetNupkgMetadataWithLicenseUrl_ReturnsLicenseUrl()
        {
            // Arrange
            var stream = TestHelper.GetStream("Newtonsoft.Json.9.0.2-beta1.nupkg.testdata");
            var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);
            var baseUrl = "http://example/";
            var uriNodeName = new Uri(String.Concat(baseUrl, "newtonsoft.json.9.0.2-beta1.json"));

            // Act
            var graph = Utils.CreateNuspecGraph(metadata.Nuspec, baseUrl, normalizeXml: true);
            var licenseFileTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "licenseFile"))));
            var licenseExpressionTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "licenseExpression"))));
            var licenseUrlTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "licenseUrl"))));
            
            // Assert
            Assert.Empty(licenseFileTriples);
            Assert.Empty(licenseExpressionTriples);
            Assert.Single(licenseUrlTriples);
        }

        [Theory]
        [InlineData("TestPackage.LicenseExpression.0.1.0.nupkg.testdata", "licenseExpression", "MIT", 0)]
        [InlineData("TestPackage.LicenseFile.0.1.0.nupkg.testdata", "licenseFile", "license.txt", 0)]
        [InlineData("TestPackage.LicenseExpressionAndUrl.0.1.0.nupkg.testdata", "licenseExpression", "MIT", 1)]
        [InlineData("TestPackage.LicenseFileAndUrl.0.1.0.nupkg.testdata", "licenseFile", "license.txt", 1)]
        public void GetNupkgMetadataWithLicenseType_ReturnsLicense(string packageName, string licenseType, string licenseContent, int expectedLicenseUrlNumber)
        {
            // Arrange
            var stream = TestHelper.GetStream(packageName);
            var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);
            var baseUrl = "http://example/";
            var uriNodeName = new Uri(String.Concat(baseUrl, "testpackage.license.0.1.0.json"));

            // Act
            var graph = Utils.CreateNuspecGraph(metadata.Nuspec, baseUrl, normalizeXml: true);
            var licenseTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet, licenseType))));
            var licenseUrlTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "licenseUrl"))));
            var result = (LiteralNode)licenseTriples.First().Object;

            // Assert
            Assert.Equal(expectedLicenseUrlNumber, licenseUrlTriples.Count());
            Assert.Single(licenseTriples);
            Assert.Equal(licenseContent, result.Value);
        }

        [Theory]
        [InlineData("TestPackage.IconAndIconUrl.0.4.2.nupkg.testdata", true, true)]
        [InlineData("TestPackage.IconOnlyEmptyType.0.4.2.nupkg.testdata", false, false)]
        [InlineData("TestPackage.IconOnlyFileType.0.4.2.nupkg.testdata", true, false)]
        [InlineData("TestPackage.IconOnlyInvalidType.0.4.2.nupkg.testdata", false, false)]
        [InlineData("TestPackage.IconOnlyNoType.0.4.2.nupkg.testdata", true, false)]
        public void GetNupkgMetadataWithIcon_ProcessesCorrectly(string packageFilename, bool expectedIconMetadata, bool expectedIconUrlMetadata)
        {
            // Arrange
            var stream = TestHelper.GetStream(packageFilename);
            var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);
            var baseUrl = "http://example/";
            var packageIdVersion = packageFilename.Replace(".nupkg.testdata", "").ToLowerInvariant();
            var uriNodeName = new Uri(string.Concat(baseUrl, packageIdVersion, ".json"));

            // Act
            var graph = Utils.CreateNuspecGraph(metadata.Nuspec, baseUrl, normalizeXml: true);
            var iconTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "iconFile"))));
            var iconUrlTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "iconUrl"))));
            var result = (LiteralNode)iconTriples.FirstOrDefault()?.Object;

            // Assert
            if (expectedIconMetadata)
            {
                Assert.Single(iconTriples);
                Assert.Equal("icon.png", result.Value);
            }
            else
            {
                Assert.Empty(iconTriples);
            }

            if (expectedIconUrlMetadata)
            {
                Assert.Single(iconUrlTriples);
            }
            else
            {
                Assert.Empty(iconUrlTriples);
            }
        }

        [Theory]
        [InlineData("TestPackage.readmeFileOnly.0.4.2.nupkg.testdata")]
        [InlineData("TestPackage.readmeWithNoType.0.4.2.nupkg.testdata")]
        public void GetNupkgMetadataWithReadme_ProcessesCorrectly(string packageFilename)
        {
            // Arrange
            var stream = TestHelper.GetStream(packageFilename);
            var metadata = Utils.GetNupkgMetadata(stream, packageHash: null);
            var baseUrl = "http://example/";
            var packageIdVersion = packageFilename.Replace(".nupkg.testdata", "").ToLowerInvariant();
            var uriNodeName = new Uri(string.Concat(baseUrl, packageIdVersion, ".json"));

            // Act
            var graph = Utils.CreateNuspecGraph(metadata.Nuspec, baseUrl, normalizeXml: true);
            var readmeTriples = graph.GetTriplesWithSubjectPredicate(
                graph.CreateUriNode(uriNodeName),
                graph.CreateUriNode(new Uri(String.Concat(Schema.Prefixes.NuGet + "readmeFile"))));
            var result = (LiteralNode)readmeTriples.FirstOrDefault()?.Object;

            // Assert
            Assert.Single(readmeTriples);
            Assert.Equal("readme.md", result.Value);
        }
    }
}