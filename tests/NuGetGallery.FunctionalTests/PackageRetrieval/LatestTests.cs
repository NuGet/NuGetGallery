// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.PackageRetrieval
{
    public class LatestTests : GalleryTestBase
    {
        /// <inheritdoc />
        public LatestTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task LatestIsAccessible()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageId}/latest");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
                Assert.Equal("application/html; charset=utf-8", contentType);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task LatestPreReleaseIsAccessible()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageId}/latest/prerelease");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
                Assert.Equal("application/html; charset=utf-8", contentType);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task LatestPreReleaseWithSuffixIsAccessible()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageId}/latest/prerelease/alpha");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
                Assert.Equal("application/html; charset=utf-8", contentType);
            }
        }
    }
}