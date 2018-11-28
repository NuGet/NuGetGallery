// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class PackageValidatorContextTests
    {
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity(id: "a", version: new NuGetVersion("1.0.0"));
        private static readonly FeedPackageIdentity _feedPackageIdentity = new FeedPackageIdentity(_packageIdentity);

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
                    _feedPackageIdentity,
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
                    _packageIdentity)
            };
            var context = new PackageValidatorContext(_feedPackageIdentity, catalogEntries);

            Assert.Same(_feedPackageIdentity, context.Package);
            Assert.Same(catalogEntries, context.CatalogEntries);
        }
    }
}