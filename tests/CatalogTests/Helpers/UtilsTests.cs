// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetResource_WhenResourceNameIsNullOrEmpty_Throws(string resourceName)
        {
            var exception = Assert.Throws<ArgumentException>(() => Utils.GetResource(resourceName));

            Assert.Equal("resourceName", exception.ParamName);
        }

        [Fact]
        public void GetResource_WhenResourceNameIsValid_ReturnsString()
        {
            var resource = Utils.GetResource("sparql.SelectInlinePackage.rq");

            Assert.NotEmpty(resource);
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
            using (var stream = Utils.GetResourceStream("sparql.SelectInlinePackage.rq"))
            {
                Assert.NotNull(stream);
            }
        }

        private static MemoryStream GetPackageStream()
        {
            return TestHelper.GetStream("Newtonsoft.Json.9.0.2-beta1.nupkg");
        }
    }
}