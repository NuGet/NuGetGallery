// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class LicenseFileFlatContainerServiceFacts
    {
        private Uri _packageBaseAddressUri = new Uri("https://test.org");
        private ILicenseFileFlatContainerService createService(Mock<IServiceDiscoveryClient> serviceDiscoveryClient = null)
        {
            if (serviceDiscoveryClient == null)
            {
                var uriList = new List<Uri>()
                {
                    _packageBaseAddressUri
                };
                serviceDiscoveryClient = new Mock<IServiceDiscoveryClient>();
                serviceDiscoveryClient.Setup(c => c.GetEndpointsForResourceType(It.IsAny<string>())).Returns(Task.FromResult<IEnumerable<Uri>>(uriList));
            }

            return new LicenseFileFlatContainerService(serviceDiscoveryClient.Object);
        }

        [Fact]
        public async Task GivenNullPackageIdThrowsException()
        {
            // Arrange
            string packageId = null;
            var licenseFileFlatContainerService = createService();
            
            // Act
            var exception =  await Assert.ThrowsAsync<ArgumentNullException>(async () => await licenseFileFlatContainerService.GetLicenseFileFlatContainerPathAsync(packageId, "1.0.0"));
            
            // Assert
            Assert.Equal(nameof(packageId), exception.ParamName);
        }

        [Fact]
        public async Task GivenNullpackageVersionThrowsException()
        {
            // Arrange
            string packageVersion = null;
            var licenseFileFlatContainerService = createService();
            
            // Act
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await licenseFileFlatContainerService.GetLicenseFileFlatContainerPathAsync("packageId", packageVersion));

            // Assert
            Assert.Equal(nameof(packageVersion), exception.ParamName);
        }

        [Fact]
        public async Task GivenPackageIdAndVersionReturnFlatContainerUrl()
        {
            // Arrange
            var packageId = "packageId";
            var packageVersion = "1.0.0";
            var licenseFileFlatContainerService = createService();

            // Act
            var licenseFileFlatContainerPath = await licenseFileFlatContainerService.GetLicenseFileFlatContainerPathAsync(packageId, packageVersion);

            // Assert
            var expectedLicenseFileUriBuilder = new UriBuilder(_packageBaseAddressUri);
            expectedLicenseFileUriBuilder.Path = string.Join("/", new string[] { packageId.ToLowerInvariant(), NuGetVersionFormatter.Normalize(packageVersion).ToLowerInvariant(), CoreConstants.LicenseFileName });
            var expectedLicenseFileFlatContainerPath = expectedLicenseFileUriBuilder.Uri.ToString();

            Assert.Equal(expectedLicenseFileFlatContainerPath, licenseFileFlatContainerPath);
        }
    }
}