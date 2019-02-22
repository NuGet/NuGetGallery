// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.AtomFeed
{
    public class AtomFeedTests : GalleryTestBase
    {
        public AtomFeedTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task IsParsableForAvailablePackage()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/{Constants.TestPackageId}/atom.xml");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
                Assert.Equal("application/atom+xml; charset=utf-8", contentType);

                var feed = await ReadFeedAsync(response);
                Assert.Contains(Constants.TestPackageId, feed.Title.Text);
                Assert.NotEmpty(feed.Items);
                foreach (var item in feed.Items)
                {
                    Assert.Contains(Constants.TestPackageId, item.Title.Text);
                }
            }
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ObservesPrerelParameter(bool prerel)
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/Newtonsoft.Json/atom.xml?prerel={prerel}");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                var feed = await ReadFeedAsync(response);
                foreach (var item in feed.Items)
                {
                    var version = item.Title.Text.Split(' ').Last();
                    Assert.True(NuGetVersion.TryParse(version, out var parsedVersion), $"The version '{version}' is not parsable.");
                    if (!prerel)
                    {
                        Assert.False(parsedVersion.IsPrerelease, $"The version '{version}' should not be included since it is prerelease.");
                    }
                }
            }
        }

        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task DefaultsToIncludingPrerel()
        {
            // Arrange
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/Newtonsoft.Json/atom.xml");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                var feed = await ReadFeedAsync(response);
                var prerelCount = 0;
                foreach (var item in feed.Items)
                {
                    var version = item.Title.Text.Split(' ').Last();
                    Assert.True(NuGetVersion.TryParse(version, out var parsedVersion), $"The version '{version}' is not parsable.");
                    if (parsedVersion.IsPrerelease)
                    {
                        prerelCount++;
                    }
                }

                Assert.InRange(prerelCount, 1, feed.Items.Count());
            }
        }

        [Fact]
        [Priority(2)]
        [Category("P2Tests")]
        public async Task DoesNotExistForPackageThatDoesNotExist()
        {
            // Arrange
            // This package ID can never exist since an ID can't have two hyphens next to each other.
            var feedUrl = new Uri(new Uri(UrlHelper.BaseUrl), $"/packages/Base--TestPackage/atom.xml");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
                Assert.Equal("text/html; charset=utf-8", contentType);
            }
        }

        private static async Task<SyndicationFeed> ReadFeedAsync(HttpResponseMessage response)
        {
            var formatter = new Atom10FeedFormatter();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
            }))
            {
                formatter.ReadFrom(xmlReader);
            }

            return formatter.Feed;
        }
    }
}