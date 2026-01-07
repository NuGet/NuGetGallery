// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.PackageRetrieval
{
    public class LatestTests : GalleryTestBase
    {
        private readonly Regex MetaDataTitleExpression = new Regex(@"<meta property=""og:title"" content=""(?<package>[^\s]+) (?<version>[^""]+)"" />"); 

        /// <inheritdoc />
        public LatestTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        private void GetMetaDataOrFail(string content, out string package, out string version)
        {
            Assert.True(MetaDataTitleExpression.IsMatch(content), "Failed to find metadata property 'og:title' in the html content.");
            var match = MetaDataTitleExpression.Match(content);
            package = match.Groups["package"].Value;
            version = match.Groups["version"].Value;
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [InlineData(Constants.TestPackageId, "1.0.0")]
        [InlineData(Constants.TestPackageIdWithPrereleases, "1.4.0-delta.4")]
        [InlineData(Constants.TestPackageIdNoStable, "1.0.0-beta")]
        public async Task AbsoluteLatestReturnsLatestEvenIfItIsPrerelease(string id, string expectedVersion)
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{id}/absoluteLatest");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(id, package);
                Assert.Equal(expectedVersion, version);
            }
        }

        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task LatestReturnsLatestNonPrerelease()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdWithPrereleases}/latest");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdWithPrereleases, package);
                Assert.Equal("1.3.0", version);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task PreReleaseIsLatestPrereleasePackage()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdWithPrereleases}/latest/prerelease");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdWithPrereleases, package);
                Assert.Equal("1.4.0-delta.4", version);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task LatestPrereleaseWhichDoesNotExistFallsBackToLatestPrerelease()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdWithPrereleases}/latest/prerelease/nonexist");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdWithPrereleases, package);
                Assert.Equal("1.4.0-delta.4", version);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task ShouldReturnLatestDeltaPackage()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdWithPrereleases}/latest/prerelease/delta");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdWithPrereleases, package);
                Assert.Equal("1.4.0-delta.4", version);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task ShouldReturnLatestBetaPackage()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdWithPrereleases}/latest/prerelease/beta");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdWithPrereleases, package);
                Assert.Equal("1.2.0-beta", version);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task FallbackToPrereleaseForNonExistant()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdNoStable}/5.0.0");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdNoStable, package);
                Assert.Equal("1.0.0-beta", version);
            }
        }

        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task FallbackToStable()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageId}/5.0.0");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageId, package);
                Assert.Equal("1.0.0", version);
            }
        }


        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task FallbackPrefersStableOverPrerelease()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageIdWithPrereleases}/5.0.0");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                GetMetaDataOrFail(content, out var package, out var version);
                Assert.Equal(Constants.TestPackageIdWithPrereleases, package);
                Assert.Equal("1.3.0", version);
            }
        }

        private static void AssertIsHtml(HttpResponseMessage response)
        {
            var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
            Assert.Equal("text/html; charset=utf-8", contentType);
        }
    }
}