// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class PackageDeprecationItemTests
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsIfNullReasons()
            {
                Assert.Throws<ArgumentNullException>(() => new PackageDeprecationItem(null, null, null, null));
            }

            [Fact]
            public void ThrowsIfEmptyReasons()
            {
                Assert.Throws<ArgumentException>(() => new PackageDeprecationItem(new string[0], null, null, null));
            }

            [Fact]
            public void ThrowsIfVersionRangeProvidedWithoutId()
            {
                Assert.Throws<ArgumentException>(() => new PackageDeprecationItem(new[] { "first", "second" }, null, null, "howdy"));
            }

            [Fact]
            public void ThrowsIfIdProvidedWithoutVersionRange()
            {
                Assert.Throws<ArgumentException>(() => new PackageDeprecationItem(new[] { "first", "second" }, null, "howdy", null));
            }

            [Fact]
            public void SetsExpectedValues()
            {
                var reasons = new[] { "first", "second" };
                var message = "message";
                var id = "theId";
                var versionRange = "homeOnTheRange";

                var deprecation = new PackageDeprecationItem(reasons, message, id, versionRange);
                Assert.Equal(reasons, deprecation.Reasons);
                Assert.Equal(message, deprecation.Message);
                Assert.Equal(id, deprecation.AlternatePackageId);
                Assert.Equal(versionRange, deprecation.AlternatePackageRange);
            }
        }
    }
}
