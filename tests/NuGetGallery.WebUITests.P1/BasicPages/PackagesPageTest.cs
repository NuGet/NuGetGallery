// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.WebUITests.BasicPages
{
    /// <summary>
    /// Sends http request to individual package pages and checks the response for appropriate title and download count.
    /// priority : p1
    /// </summary>
    public class PackagesPageTest
    {
        [Priority(1)]
        [Fact]
        public async Task PackagePageContainsPackageIdAndVersion()
        {
            // Arrange
            using var client = new HttpClient();
            var packageId = Constants.TestPackageId;
            var latestVersion = ClientSdkHelper.GetLatestStableVersion(packageId);
            
            // Act
            var response = await client.GetAsync(UrlHelper.BaseUrl + @"/Packages/" + packageId);
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            // Check that the title contains package id and latest stable version
            Assert.Contains(packageId + " " + latestVersion, content);
        }
    }
}
