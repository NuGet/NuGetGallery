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
    public class StatsInHomePageTest : NuGetFluentTest
    {
        public StatsInHomePageTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Cross-check the contents of the Statistics on the homepage against the stats/total API endpoint.")]
        [Priority(1)]
        public async Task StatsInHomePage()
        {
            // Request the last 6 weeks endpoint.
            var request = WebRequest.Create(UrlHelper.BaseUrl + @"/stats/totals");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            // Extract the substrings we'll search for on the front page.
            string downloads = responseText.Substring(responseText.IndexOf(@"Downloads"":""", StringComparison.Ordinal) + 12);
            downloads = downloads.Substring(0, downloads.IndexOf(@"""", StringComparison.Ordinal));
            string uniquePackages = responseText.Substring(responseText.IndexOf(@"UniquePackages"":""", StringComparison.Ordinal) + 17);
            uniquePackages = uniquePackages.Substring(0, uniquePackages.IndexOf(@"""", StringComparison.Ordinal));
            string totalPackages = responseText.Substring(responseText.IndexOf(@"TotalPackages"":""", StringComparison.Ordinal) + 16);
            totalPackages = totalPackages.Substring(0, totalPackages.IndexOf(@"""", StringComparison.Ordinal));

            I.Open(UrlHelper.BaseUrl);
            I.Wait(5);
            I.Expect.Text(downloads).In("#Downloads");
            I.Expect.Text(uniquePackages).In("#UniquePackages");
            I.Expect.Text(totalPackages).In("#TotalPackages");
        }
    }
}
