// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Statistics
{
    public class PackageStatisticsTests
    {
        public PackageStatisticsTests()
        {
            // Suppress SSL validation for *.cloudapp.net.
            ServicePointManagerInitializer.InitializeServerCertificateValidationCallback();
        }

        /// <summary>
        /// Double-checks whether expected fields exist in the packages feed.
        /// </summary>
        [Fact]
        [Description("Verify the webresponse for stats/downloads/last6weeks/ returns all 6 fields")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackageFeedStatsSanityTest()
        {
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
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
        [Fact]
        [Description("Verify the webresponse for stats/downloads/last6weeks/ contains the right amount of packages")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackageFeedCountParameterTest()
        {
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            string[] separators = { "}," };
            int packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            // Only verify the stats feed contains 500 packages for production
            if (UrlHelper.BaseUrl.ToLowerInvariant() == Constants.NuGetOrgUrl)
            {
                Assert.True(packageCount == 500, "Expected feed to contain 500 packages. Actual count: " + packageCount);
            }

            request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks?count=5");
            // Get the response.
            response = await request.GetResponseAsync();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.True(packageCount == 5, "Expected feed to contain 5 packages. Actual count: " + packageCount);
        }
    }
}
