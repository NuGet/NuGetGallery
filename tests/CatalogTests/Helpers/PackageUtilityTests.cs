// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;

namespace CatalogTests.Helpers
{
    public class PackageUtilityTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetPackageFileName_WhenPackageIdIsNullOrEmpty_Throws(string packageId)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => PackageUtility.GetPackageFileName(packageId, packageVersion: "a"));

            Assert.Equal("packageId", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetPackageFileName_WhenPackageVersionIsNullOrEmpty_Throws(string packageVersion)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => PackageUtility.GetPackageFileName(packageId: "a", packageVersion: packageVersion));

            Assert.Equal("packageVersion", exception.ParamName);
        }

        [Theory]
        [InlineData("a", "b")]
        [InlineData("A", "B")]
        public void GetPackageFileName_WithValidArguments_ReturnsFileName(string packageId, string packageVersion)
        {
            var packageFileName = PackageUtility.GetPackageFileName(packageId, packageVersion);

            Assert.Equal($"{packageId}.{packageVersion}.nupkg", packageFileName);
        }
    }
}