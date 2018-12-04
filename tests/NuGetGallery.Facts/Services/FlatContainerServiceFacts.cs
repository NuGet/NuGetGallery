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
    public class FlatContainerServiceFacts
    {
        private IFlatContainerService CreateService(Mock<IServiceDiscoveryClient> serviceDiscoveryClient = null)
        {
            if (serviceDiscoveryClient == null)
            {
                serviceDiscoveryClient = new Mock<IServiceDiscoveryClient>();
                serviceDiscoveryClient
                    .Setup(c => c.GetEndpointsForResourceType(It.IsAny<string>()))
                    .ReturnsAsync(new List<Uri>() { new Uri("https://test.org") });
            }

            return new FlatContainerService(serviceDiscoveryClient.Object);
        }

        [Fact]
        public async Task GivenNullPackageIdThrowsException()
        {
            // Arrange
            string packageId = null;
            var licenseFileFlatContainerService = CreateService();
            
            // Act
            var exception =  await Assert.ThrowsAsync<ArgumentNullException>(async () => await licenseFileFlatContainerService.GetLicenseFileFlatContainerUrlAsync(packageId, "1.0.0"));
            
            // Assert
            Assert.Equal(nameof(packageId), exception.ParamName);
        }

        [Fact]
        public async Task GivenNullpackageVersionThrowsException()
        {
            // Arrange
            string packageVersion = null;
            var licenseFileFlatContainerService = CreateService();
            
            // Act
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await licenseFileFlatContainerService.GetLicenseFileFlatContainerUrlAsync("packageId", packageVersion));

            // Assert
            Assert.Equal(nameof(packageVersion), exception.ParamName);
        }

        [Theory]
        [InlineData("https://test.org", "https://test.org/packageid/1.2.3/license")]
        [InlineData("https://test.org/", "https://test.org/packageid/1.2.3/license")]
        [InlineData("https://test.org/flat-container", "https://test.org/flat-container/packageid/1.2.3/license")]
        [InlineData("https://test.org/flat-container/", "https://test.org/flat-container/packageid/1.2.3/license")]
        [InlineData("https://test.org/flat-container//", "https://test.org/flat-container/packageid/1.2.3/license")]
        public async Task GivenPackageIdAndVersionReturnFlatContainerUrl(string packageBaseUri, string expectedLicenseFileFlatContainerPath)
        {
            // Arrange
            var packageId = "packageId";
            var packageVersion = "01.02.03+ABC";

            var serviceDiscoveryClient = new Mock<IServiceDiscoveryClient>();
            serviceDiscoveryClient
                .Setup(c => c.GetEndpointsForResourceType(It.IsAny<string>()))
                .ReturnsAsync(new List<Uri>() { new Uri(packageBaseUri) });

            var licenseFileFlatContainerService = CreateService(serviceDiscoveryClient);

            // Act
            var licenseFileFlatContainerPath = await licenseFileFlatContainerService.GetLicenseFileFlatContainerUrlAsync(packageId, packageVersion);

            // Assert
            Assert.Equal(expectedLicenseFileFlatContainerPath, licenseFileFlatContainerPath);
        }
    }
}