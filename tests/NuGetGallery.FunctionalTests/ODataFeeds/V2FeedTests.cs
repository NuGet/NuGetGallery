// // Copyright (c) .NET Foundation. All rights reserved.
// // Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.ODataFeeds
{
    /// <summary>
    /// Checks if basic operations against V2 Feed work fine.
    /// </summary>
    public class V2FeedTests
        : GalleryTestBase
    {
        private readonly ODataHelper _odataHelper;

        public V2FeedTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _odataHelper = new ODataHelper(testOutputHelper);
        }

        [Fact]
        [Description("Verify the webresponse from /Api/V2/ feed contains the Packages text")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task ApiV2BaseUrlTest()
        {
            string expectedText1 = @"<atom:title";
            string expectedText2 = @"Packages</atom:title>";

            bool containsResponseText1 = await _odataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText1);
            Assert.True(containsResponseText1);

            bool containsResponseText2 = await _odataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText2);
            Assert.True(containsResponseText2);
        }

        [Fact]
        [Description("Verify the webresponse from /Api/V2/$metadata contains the V2FeedPackage text")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task ApiV2MetadataTest()
        {
            string expectedText = @"V2FeedPackage";
            bool containsResponseText = await _odataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl + @"$metadata", expectedText);
            Assert.True(containsResponseText);
        }

        [Fact]
        [Description("Verify the webresponse from top30 packages feed contains jQuery")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task Top30PackagesFeedTest()
        {
            string url = UrlHelper.V2FeedRootUrl + @"/Search()?$filter=IsAbsoluteLatestVersion&$orderby=DownloadCount%20desc,Id&$skip=0&$top=30&searchTerm=''&targetFramework='net45'&includePrerelease=true";
            bool containsResponseText = await _odataHelper.ContainsResponseText(url, "jQuery");
            Assert.True(containsResponseText);
        }

        [Fact]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        [Category("P0Tests")]
        [Category("ReadOnlyModeTests")]
        public async Task DownloadPackageFromV2Feed()
        {
            await _odataHelper.DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Install");
        }

        [Fact]
        [Description("Restores a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        [Category("P0Tests")]
        [Category("ReadOnlyModeTests")]
        public async Task RestorePackageFromV2Feed()
        {
            await _odataHelper.DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Restore");
        }

        [Theory]
        [InlineData("Packages?$orderby=DownloadCount+asc&$select=Id&$top=3")]
        [Description("Performs a OData request that will be rejected if not found by the search engine. The feature needs to be enabled for this test to pass.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task ODataQueryFilter(string requestParameters)
        {
            //If the search engine will be changed to handle the types of requests passed as inputs; the test inputs need to be changed.
            var requestUrl = UrlHelper.V2FeedRootUrl + requestParameters;

            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(requestUrl))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
    }
}