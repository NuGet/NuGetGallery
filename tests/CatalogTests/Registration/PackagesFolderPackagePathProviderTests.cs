// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Registration;
using Xunit;

namespace CatalogTests.Registration
{
    public class PackagesFolderPackagePathProviderTests
    {
        [Theory]
        [InlineData("Newtonsoft.Json", "9.0.1", "newtonsoft.json.9.0.1")]
        [InlineData("Newtonsoft.Json", "9.0.1+githash", "newtonsoft.json.9.0.1")]
        [InlineData("Newtonsoft.Json", "9.0.1-a", "newtonsoft.json.9.0.1-a")]
        [InlineData("Newtonsoft.Json", "9.0.1-a+githash", "newtonsoft.json.9.0.1-a")]
        [InlineData("Newtonsoft.Json", "9.0.1-a.1", "newtonsoft.json.9.0.1-a.1")]
        [InlineData("Newtonsoft.Json", "9.0.1-a.1+githash", "newtonsoft.json.9.0.1-a.1")]
        public void GetPackagePath_UsesNormalizedVersion(string id, string version, string expected)
        {
            // Arrange
            var provider = new PackagesFolderPackagePathProvider();

            // Act
            var path = provider.GetPackagePath(id, version);

            // Assert
            Assert.Equal($"packages/{expected}.nupkg", path);
        }

        [Theory]
        [InlineData("Newtonsoft.Json", "9.0.1", "newtonsoft.json/9.0.1")]
        [InlineData("Newtonsoft.Json", "9.0.1+githash", "newtonsoft.json/9.0.1")]
        [InlineData("Newtonsoft.Json", "9.0.1-a", "newtonsoft.json/9.0.1-a")]
        [InlineData("Newtonsoft.Json", "9.0.1-a+githash", "newtonsoft.json/9.0.1-a")]
        [InlineData("Newtonsoft.Json", "9.0.1-a.1", "newtonsoft.json/9.0.1-a.1")]
        [InlineData("Newtonsoft.Json", "9.0.1-a.1+githash", "newtonsoft.json/9.0.1-a.1")]
        public void GetIconPath_UsesNormalizedVersion(string id, string version, string expected)
        {
            // Arrange
            var provider = new PackagesFolderPackagePathProvider();

            // Act
            var path = provider.GetIconPath(id, version);

            // Assert
            Assert.Equal($"packages/{expected}/icon", path);
        }
    }
}
