// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.XunitExtensions;
using Xunit;

namespace NuGetGallery.FunctionalTests.Statistics
{
    public class PackageStatisticsTests
    {
        /// <summary>
        /// Double-checks whether expected fields exist in the packages feed.
        /// </summary>
        [NeedsStatisticsServiceFact]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackageFeedStatsConfidenceTest()
        {
            var requestUrl = UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/";

            string responseText;
            using (var httpClient = new HttpClient())
            {
                responseText = await httpClient.GetStringAsync(requestUrl);
            }

            string firstPackage = responseText.Substring(responseText.IndexOf("{"), responseText.IndexOf("}") - responseText.IndexOf("{"));

            Assert.True(firstPackage.Contains(@"""PackageId"": """), "Expected PackageId field is missing.");
            Assert.True(firstPackage.Contains(@"""PackageVersion"": """), "Expected PackageVersion field is missing.");
            Assert.True(firstPackage.Contains(@"""Gallery"": """), "Expected Gallery field is missing.");
            Assert.True(firstPackage.Contains(@"""PackageTitle"": """), "Expected PackageTitle field is missing.");
            Assert.True(firstPackage.Contains(@"""PackageIconUrl"": """), "Expected PackageIconUrl field is missing.");
            Assert.True(firstPackage.Contains(@"""Downloads"": "), "Expected PackageIconUrl field is missing.");
        }

        /// <summary>
        /// Verify copunt querystring parameter in the Packages feed.
        /// </summary>
        [NeedsStatisticsServiceFact]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackageFeedCountParameterTest()
        {
            using (var httpClient = new HttpClient())
            {
                var requestUrl = UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/";
                var responseText = await httpClient.GetStringAsync(requestUrl);

                string[] separators = { "}," };
                int packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
                // Only verify the stats feed contains 500 packages for production
                if (UrlHelper.BaseUrl.ToLowerInvariant() == Constants.NuGetOrgUrl)
                {
                    Assert.True(packageCount == 500, "Expected feed to contain 500 packages. Actual count: " + packageCount);
                }

                requestUrl = UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks?count=5";

                // Get the response.
                responseText = await httpClient.GetStringAsync(requestUrl);

                packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.True(packageCount == 5, "Expected feed to contain 5 packages. Actual count: " + packageCount);
            }
        }
    }
}
