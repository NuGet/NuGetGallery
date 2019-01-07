// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

        public static IEnumerable<object[]> Constructor_WhenArgumentsAreValid_InitializesInstance_Data
        {
            get
            {
                yield return new object[] { null };

                yield return new object[] {
                    new[]
                    {
                        new CatalogIndexEntry(
                            new Uri("https://nuget.test/a"),
                            CatalogConstants.NuGetPackageDetails,
                            Guid.NewGuid().ToString(),
                            DateTime.UtcNow,
                            _packageIdentity)
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(Constructor_WhenArgumentsAreValid_InitializesInstance_Data))]
        public void Constructor_WhenArgumentsAreValid_InitializesInstance(CatalogIndexEntry[] catalogEntries)
        {
            var context = new PackageValidatorContext(_feedPackageIdentity, catalogEntries);

            Assert.Same(_feedPackageIdentity, context.Package);
            Assert.Same(catalogEntries, context.CatalogEntries);
        }
    }
}