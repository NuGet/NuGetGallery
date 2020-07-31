// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;

namespace CatalogTests.Helpers
{
    public class PackageContentUriBuilderTests
    {
        [Fact]
        public void PlaceholdersDidNotChange()
        {
            Assert.Equal("{id-lower}", PackageContentUriBuilder.IdLowerPlaceholderString);
            Assert.Equal("{version-lower}", PackageContentUriBuilder.VersionLowerPlaceholderString);
        }

        public class TheConstructor
        {
            [Fact]
            public void ThrowsForNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => new PackageContentUriBuilder(null));
            }
        }

        public class TheBuildMethod
        {
            [Theory]
            [InlineData(null, "1.0.0")]
            [InlineData("Package.Id", null)]
            public void ThrowsForNullArguments(string packageId, string normalizedPackageVersion)
            {
                var packageContentUriBuilder = new PackageContentUriBuilder("https://unittest.org/packages/{id-lower}/{version-lower}.nupkg");

                Assert.Throws<ArgumentNullException>(() => packageContentUriBuilder.Build(packageId, normalizedPackageVersion));
            }

            [Fact]
            public void ProperlyBuildsPackageContentUrl()
            {
                // Arrange
                var packageContentUriBuilder = new PackageContentUriBuilder("https://unittest.org/packages/{id-lower}/{version-lower}.nupkg");
                var expectedUrl = new Uri("https://unittest.org/packages/package.id/1.0.0-alpha.1.nupkg");

                // Act
                var actualUrl = packageContentUriBuilder.Build("Package.Id", "1.0.0-Alpha.1");

                // Assert
                Assert.Equal(expectedUrl, actualUrl);
            }
        }
    }
}