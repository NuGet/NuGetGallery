// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.ODataFeeds
{
    public class SearchTest
        : GalleryTestBase
    {
        public SearchTest(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Performs a querystring-based search of the v1 feed. Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchV1Feed()
        {
            await SearchFeedAsync(UrlHelper.V1FeedRootUrl, "Json.NET");
        }

        [Fact]
        [Description("Performs a querystring-based search of the v2 feed. Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchV2Feed()
        {
            await SearchFeedAsync(UrlHelper.V2FeedRootUrl, "Json.NET");
        }

        private async Task SearchFeedAsync(string feedRootUrl, string title)
        {
            var requestUrl = feedRootUrl + @"Search()?$filter=IsLatestVersion&$skip=0&$top=25&searchTerm='newtonsoft%20json'&targetFramework='net40'&includePrerelease=false";
            TestOutputHelper.WriteLine("Request: GET " + requestUrl);

            string responseText;
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(requestUrl))
            {
                TestOutputHelper.WriteLine("Response: HTTP " + response.StatusCode);

                responseText = await response.Content.ReadAsStringAsync();
            }

            var expectedUrl = feedRootUrl + "package/Newtonsoft.Json/";

            Assert.True(responseText.Contains(@"<title type=""text"">" + title + @"</title>")
                     || responseText.Contains(@"<d:Title>" + title + @"</d:Title>"), "The expected package title '" + title + "' wasn't found in the feed. Feed contents: " + responseText);
            Assert.True(responseText.Contains(@"<content type=""application/zip"" src=""" + expectedUrl), "The expected package URL '" + expectedUrl + "' wasn't found in the feed.  Feed contents: " + responseText);
            Assert.False(responseText.Contains(@"jquery"), "The feed contains non-matching package names. Feed contents: " + responseText);
        }
    }
}
