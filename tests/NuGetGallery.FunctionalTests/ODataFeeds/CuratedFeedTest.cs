// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.ODataFeeds
{
    public class CuratedFeedTest
        : GalleryTestBase
    {
        private readonly CommandlineHelper _commandlineHelper;
        private readonly ClientSdkHelper _clientSdkHelper;
        private readonly PackageCreationHelper _packageCreationHelper;

        public CuratedFeedTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _commandlineHelper = new CommandlineHelper(TestOutputHelper);
            _clientSdkHelper = new ClientSdkHelper(TestOutputHelper);
            _packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
        }

        [Fact]
        [Description("Performs a querystring-based search of the Microsoft curated feed. Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchMicrosoftDotNetCuratedFeed()
        {
            var packageId = "microsoft.aspnet.webpages";
            var requestUrl = UrlHelper.DotnetCuratedFeedUrl + @"Search()?searchTerm='packageid%3A" + packageId + "'";

            string responseText;
            using (var httpClient = new HttpClient())
            {
                responseText = await httpClient.GetStringAsync(requestUrl);
            }

            string packageUrl = @"<id>" + UrlHelper.DotnetCuratedFeedUrl + "Packages(Id='" + packageId;
            Assert.Contains(packageUrl.ToLowerInvariant(), responseText.ToLowerInvariant());
        }

        [Fact]
        [Description("Validates the microsoftdotnet feed returns packages via FindPackagesById")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task ValidateMicrosoftDotNetCuratedFeed()
        {
            using (var httpClient = new HttpClient())
            {
                // Validate that FindPackagesById returns entries for a known seeded package.
                var requestUrl = UrlHelper.DotnetCuratedFeedUrl + "FindPackagesById()?id='BaseTestPackage.SearchFilters'";

                string responseText;
                using (var response = await httpClient.GetAsync(requestUrl))
                {
                    responseText = await response.Content.ReadAsStringAsync();
                }

                // Make sure that at least 1 entry is returned.
                int entryCount = responseText.Split(new[] { "<entry>" }, StringSplitOptions.RemoveEmptyEntries).Length - 1;
                Assert.True(entryCount >= 1, "Expected at least 1 entry but found " + entryCount);
            }
        }
    }
}
