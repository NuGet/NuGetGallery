// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.IO;
using System.Net;
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
        [Description("Performs a querystring-based search of the v1 feed.  Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchV1Feed()
        {
            await SearchFeedAsync(UrlHelper.V1FeedRootUrl, "ASP.NET Web Helpers Library");
        }

        [Fact]
        [Description("Performs a querystring-based search of the default (non-curated) v2 feed.  Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchV2Feed()
        {
            await SearchFeedAsync(UrlHelper.V2FeedRootUrl, "microsoft-web-helpers");
        }

        private async Task SearchFeedAsync(string feedRootUrl, string title)
        {
            var requestUrl = feedRootUrl + @"Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='web%20helpers'&targetFramework='net40'&includePrerelease=false";
            TestOutputHelper.WriteLine("Request: GET " + requestUrl);

            var request = WebRequest.Create(requestUrl);
            var response = (HttpWebResponse) await request.GetResponseAsync();

            TestOutputHelper.WriteLine("Response: HTTP " + response.StatusCode);

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            Assert.True(responseText.Contains(@"<title type=""text"">" + title + @"</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.True(responseText.Contains(@"<content type=""application/zip"" src=""" + feedRootUrl + "package/microsoft-web-helpers/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.False(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);
        }
    }
}
