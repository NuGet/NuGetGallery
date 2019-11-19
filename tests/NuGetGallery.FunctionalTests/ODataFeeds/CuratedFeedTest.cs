// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
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
            var requestUrl = UrlHelper.DotnetCuratedFeedUrl + @"Packages()?$filter=tolower(Id)%20eq%20'" + packageId + "'&$orderby=Id&$skip=0&$top=30";

            string responseText;
            using (var httpClient = new HttpClient())
            {
                responseText = await httpClient.GetStringAsync(requestUrl);
            }

            string packageUrl = @"<id>" + UrlHelper.DotnetCuratedFeedUrl + "Packages(Id='" + packageId;
            Assert.Contains(packageUrl.ToLowerInvariant(), responseText.ToLowerInvariant());
        }

        [Fact]
        [Description("Validates the microsoftdotnet feed, including the next page link")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task ValidateMicrosoftDotNetCuratedFeed()
        {
            using (var httpClient = new HttpClient())
            {
                var requestUrl = UrlHelper.DotnetCuratedFeedUrl + "Packages";

                string responseText;
                using (var response = await httpClient.GetAsync(requestUrl))
                {
                    responseText = await response.Content.ReadAsStringAsync();
                }

                // Make sure that 100 entries are returned.  This means that if we split on the <entry> tag, we'd have 101 strings.
                int length = responseText.Split(new[] { "<entry>" }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.True(length == 101, "An unexpected number of entries was found.  Actual number was " + (length - 1));

                // Get the link to the next page.
                string link = responseText.Split(new[] { @"<link rel=""next"" href=""" }, StringSplitOptions.RemoveEmptyEntries)[1];
                link = link.Substring(0, link.IndexOf(@"""", StringComparison.Ordinal));

                // Get the response.
                using (var response = await httpClient.GetAsync(link))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception($"Next page link is broken. Expected 200, got '{response.StatusCode}'");
                    }
                }
            }
        }
    }
}
