// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery.Configuration;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class LicenseFileBlobStorageServiceFacts
    {
        private IAppConfiguration GetConfiguration()
        {
            var mockConfiguration = new Mock<IAppConfiguration>();
            mockConfiguration.SetupGet(c => c.ServiceDiscoveryUri).Returns(new Uri("https://api.nuget.org/v3/index.json"));
            return mockConfiguration.Object;
        }

        [Fact]
        public async Task GivenNullPackageIdThrowsException()
        {
            // Arrange
            string packageId = null;
            var licenseFileBlobStorageService = new LicenseFileBlobStorageService(GetConfiguration());

            // Act
            var exception =  await Assert.ThrowsAsync<ArgumentNullException>(async () => await licenseFileBlobStorageService.GetLicenseFileBlobStoragePathAsync(packageId, "1.0.0"));
            
            // Assert
            Assert.Equal(nameof(packageId), exception.ParamName);
        }

        [Fact]
        public async Task GivenNullpackageVersionThrowsException()
        {
            // Arrange
            string packageVersion = null;
            var licenseFileBlobStorageService = new LicenseFileBlobStorageService(GetConfiguration());

            // Act
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await licenseFileBlobStorageService.GetLicenseFileBlobStoragePathAsync("packageId", packageVersion));

            // Assert
            Assert.Equal(nameof(packageVersion), exception.ParamName);
        }

        [Fact]
        public async Task GivenPackageIdAndVersionReturnBlobStorageUrl()
        {
            // Arrange
            var packageId = "packageId";
            var packageVersion = "1.0.0";
            var licenseFileBlobStorageService = new LicenseFileBlobStorageService(GetConfiguration());

            // Act
            var licenseFileBlobStoragePath = await licenseFileBlobStorageService.GetLicenseFileBlobStoragePathAsync(packageId, packageVersion);

            // Assert
            var relativePath = String.Join("/", new string[] { packageId.ToLowerInvariant(), NuGetVersionFormatter.Normalize(packageVersion).ToLowerInvariant(), CoreConstants.LicenseFileName });
            Assert.Contains(relativePath, licenseFileBlobStoragePath);
        }
    }
}