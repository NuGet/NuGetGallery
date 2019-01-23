// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests.Helpers
{
    public class LicenseHelperTests
    {
        [Theory]
        [InlineData("testPackage", "1.0.0", "https://testnuget", "https://testnuget/packages/testPackage/1.0.0/license")]
        [InlineData("testPackage", "1.0.0", "https://testnuget/", "https://testnuget/packages/testPackage/1.0.0/license")]
        [InlineData("testPackage", "1.0.0", "https://testnuget//", "https://testnuget/packages/testPackage/1.0.0/license")]
        [InlineData("testPackage", null, "https://testnuget/", null)]
        [InlineData("testPackage", "1.0.0", null, null)]
        [InlineData("", "", "https://testnuget/", null)]
        [InlineData("测试更新包", "1.0.0", "https://testnuget/", "https://testnuget/packages/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/1.0.0/license")]
        public void GivenPackageIdAndVersionAndGalleryBaseUrl_ReturnsLicenseUrl(string packageId, string packageVersion, string galleryBaseAddress, string expectedLicenseUrl)
        {
            // Arrange and Act
            var licenseUrl = LicenseHelper.GetGalleryLicenseUrl(packageId, packageVersion, galleryBaseAddress == null ? null : new Uri(galleryBaseAddress));

            // Assert
            Assert.Equal(expectedLicenseUrl, licenseUrl);
        }
    }
}
