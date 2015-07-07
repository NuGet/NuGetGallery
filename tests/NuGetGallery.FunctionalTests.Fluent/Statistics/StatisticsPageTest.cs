// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.Statistics
{
    public class StatisticsPageTest : NuGetFluentTest
    {
        public StatisticsPageTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Cross-check the contents of the Statistics page against the last6weeks API endpoint.")]
        [Priority(2)]
        public async Task StatisticsPage()
        {
            // Request the last 6 weeks endpoint.
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            // Grab the top 10 package ids in the feed.
            string[] packageName = new string[10];
            responseText = packageName[0] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """, StringComparison.Ordinal) + 14);
            packageName[0] = packageName[0].Substring(0, responseText.IndexOf(@"""", StringComparison.Ordinal));
            for (int i = 1; i < 10; i++)
            {
                responseText = packageName[i] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """, StringComparison.Ordinal) + 14);
                packageName[i] = packageName[i].Substring(0, responseText.IndexOf(@"""", StringComparison.Ordinal));
            }

            // Navigate to the stats page.
            I.Open(UrlHelper.StatsPageUrl);
            I.Expect.Url(x => x.AbsoluteUri.Contains("stats"));

            for (int i = 0; i < 10; i++)
            {
                I.Expect.Exists("td:contains('" + packageName[i] + "')");
            }
            I.Expect.Exists("p:contains('Download statistics displayed on this page reflect the actual package downloads from the NuGet.org site.')");
        }
    }
}
