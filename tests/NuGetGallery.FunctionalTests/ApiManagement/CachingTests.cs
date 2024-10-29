// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace NuGetGallery.FunctionalTests.ApiManagement
{
    public class CachingTests
    {
        private const string CacheHeaderName = "x-cache";
        private const string CacheHeaderHit = "HIT";
        private const string CacheHeaderMiss = "MISS";

        [Theory]
        [InlineData("Packages?$skip=8356600&$top=80&$filter=(tolower(Id)%20eq%20'nlog')")]
        [InlineData("Packages()?$skip=8356600&$top=80&$filter=(tolower(Id)%20eq%20'nlog')&$orderby=Id")]
        [InlineData("Packages?IsLatestVersion%20and%20substringof('sample-data',Tags)&$top=250")]
        [InlineData("Packages()?IsLatestVersion%20and%20substringof('sample-data',description)&$top=250")]
        [Category("ApiManagementTests")]
        public async Task RequestsThatShouldNotBeCachedAreNotCached(string suffix)
        {
            // Arrange
            string url = UrlHelper.V2FeedRootUrl + suffix;

            // Act
            var firstResponse = await MakeODataRequest(url);

            // sleep for 5 seconds so the clock will shift
            await Task.Delay(TimeSpan.FromSeconds(5));

            var secondResponse = await MakeODataRequest(url);

            // Assert

            Assert.Null(firstResponse.cacheHeaderValues);
            Assert.Null(secondResponse.cacheHeaderValues);

            // If there's no cache the update time should differ
            Assert.NotEqual(firstResponse.updateTime, secondResponse.updateTime);
        }

        [Theory]
        [InlineData("Packages()?$skip=0&$top=40&$filter=substringof('|{0}',Dependencies)%20or%20startswith(Dependencies,'{0}')%20or%20Id%20eq%20'{0}'&$select=DownloadCount,Id,Version")]
        [InlineData("Packages?$skip=0&$top=40&$filter=substringof('|{0}',Dependencies)%20or%20startswith(Dependencies,'{0}')%20or%20Id%20eq%20'{0}'&$select=DownloadCount,Id,Version")]
        [InlineData("Packages()?$filter=IsLatestVersion and substringof('{0}:',Dependencies)&$top=250")]
        [InlineData("Packages?$filter=IsLatestVersion and substringof('{0}:',Dependencies)&$top=250")]
        [Category("ApiManagementTests")]
        public async Task RequestsThatShouldBeCachedAreCached(string format)
        {
            // Arrange
            // Create a random request so we won't get a cached response
            string suffix = string.Format(format, Guid.NewGuid());
            string url = UrlHelper.V2FeedRootUrl + suffix;

            // Act
            var firstResponse = await MakeODataRequest(url);

            // sleep for 5 seconds so the clock will shift
            await Task.Delay(TimeSpan.FromSeconds(5));

            var secondResponse = await MakeODataRequest(url);

            // Assert
            Assert.NotNull(firstResponse.cacheHeaderValues);
            Assert.NotNull(secondResponse.cacheHeaderValues);

            Assert.Single(firstResponse.cacheHeaderValues);
            Assert.Single(secondResponse.cacheHeaderValues);

            Assert.Equal(CacheHeaderMiss, firstResponse.cacheHeaderValues.First());
            Assert.Equal(CacheHeaderHit, secondResponse.cacheHeaderValues.First());

            // Update time should be identical for a cached response
            Assert.Equal(firstResponse.updateTime, secondResponse.updateTime);
        }


        private async Task<(DateTime updateTime, IEnumerable<string> cacheHeaderValues)> MakeODataRequest(string url)
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                response.Headers.TryGetValues(CacheHeaderName, out IEnumerable<string> cacheHeaders);

                string content = await response.Content.ReadAsStringAsync();

                using (var stringReader = new StringReader(content))
                using (var xmlReader = XmlReader.Create(stringReader))
                {
                        var xmlConfig = new XmlDocument();
                        xmlConfig.XmlResolver = null;

                        xmlConfig.Load(xmlReader);
                        var updatedTime = xmlConfig.GetElementsByTagName("updated")[0].InnerText;

                        return (DateTime.Parse(updatedTime), cacheHeaders);
                }
            }
        }
    }
}
