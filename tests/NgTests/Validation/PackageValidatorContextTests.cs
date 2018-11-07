// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class PackageValidatorContextTests
    {
        private static readonly FeedPackageIdentity _package = new FeedPackageIdentity(id: "a", version: "1.0.0");

        [Fact]
        public void Constructor_WhenPackageIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PackageValidatorContext(
                    package: null,
                    catalogEntries: Enumerable.Empty<CatalogIndexEntry>()));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenCatalogEntriesIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PackageValidatorContext(
                    _package,
                    catalogEntries: null));

            Assert.Equal("catalogEntries", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_InitializesInstance()
        {
            var catalogEntries = new[]
            {
                new CatalogIndexEntry(
                    new Uri("https://nuget.test/a"),
                    CatalogConstants.NuGetPackageDetails,
                    Guid.NewGuid().ToString(),
                    DateTime.UtcNow,
                    "a",
                    new NuGetVersion("1.0.0"))
            };
            var context = new PackageValidatorContext(_package, catalogEntries);

            Assert.Same(_package, context.Package);
            Assert.Same(catalogEntries, context.CatalogEntries);
        }
    }
}