// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
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
            var request = WebRequest.Create(UrlHelper.DotnetCuratedFeedUrl + @"Packages()?$filter=tolower(Id)%20eq%20'" + packageId + "'&$orderby=Id&$skip=0&$top=30");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            string packageUrl = @"<id>" + UrlHelper.DotnetCuratedFeedUrl + "Packages(Id='" + packageId;
            Assert.True(responseText.ToLowerInvariant().Contains(packageUrl.ToLowerInvariant()));
        }

        [Fact]
        [Description("Performs a querystring-based search of the Windows 8 curated feed. Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchWindows8CuratedFeed()
        {
            string packageName = "NuGetGallery.FunctionalTests.SearchWindows8CuratedFeed";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();

            int exitCode = await UploadPackageToCuratedFeed(packageName, version, FeedType.Windows8CuratedFeed);
            Assert.True((exitCode == 0), Constants.UploadFailureMessage);

            bool applied = CheckPackageExistInCuratedFeed(packageName, FeedType.Windows8CuratedFeed);
            var userMessage = string.Format(Constants.PackageNotFoundAfterUpload, packageName, UrlHelper.Windows8CuratedFeedUrl);
            Assert.True(applied, userMessage);
        }

        [Fact]
        [Description("Performs a querystring-based search of the WebMatrix curated feed.  Confirms expected packages are returned.")]
        [Priority(0)]
        [Category("P0Tests")]
        public async Task SearchWebMatrixCuratedFeed()
        {
            string packageName = "NuGetGallery.FunctionalTests.SearchWebMatrixCuratedFeed";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();

            int exitCode = await UploadPackageToCuratedFeed(packageName, version, FeedType.WebMatrixCuratedFeed);
            Assert.True((exitCode == 0), Constants.UploadFailureMessage);

            bool applied = CheckPackageExistInCuratedFeed(packageName, FeedType.WebMatrixCuratedFeed);
            var userMessage = string.Format(Constants.PackageNotFoundAfterUpload, packageName, UrlHelper.WebMatrixCuratedFeedUrl);
            Assert.True(applied, userMessage);
        }

        [Fact]
        [Description("Checks the MicrosoftDotNet curated feed for duplicate packages.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task CheckMicrosoftDotNetCuratedFeedForDuplicates()
        {
            await CheckCuratedFeedForDuplicates(FeedType.DotnetCuratedFeed);
        }

        [Fact(Skip = "This can be run manually if required as it takes a very long time to run.")]
        [Description("Checks the WebMatrix curated feed for duplicate packages.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task CheckWebMatrixCuratedFeedForDuplicates()
        {
            await CheckCuratedFeedForDuplicates(FeedType.WebMatrixCuratedFeed);
        }

        [Fact]
        [Description("Checks the Windows8 curated feed for duplicate packages.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task CheckWindows8CuratedFeedForDuplicates()
        {
            await CheckCuratedFeedForDuplicates(FeedType.Windows8CuratedFeed);
        }

        [Fact]
        [Description("Validates the microsoftdotnet feed, including the next page link")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task ValidateMicrosoftDotNetCuratedFeed()
        {
            var request = WebRequest.Create(GetCuratedFeedUrl(FeedType.DotnetCuratedFeed) + "Packages");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            // Make sure that 40 entries are returned.  This means that if we split on the <entry> tag, we'd have 41 strings.
            int length = responseText.Split(new[] { "<entry>" }, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.True(length == 41, "An unexpected number of entries was found.  Actual number was " + (length - 1));

            // Get the link to the next page.
            string link = responseText.Split(new[] { @"<link rel=""next"" href=""" }, StringSplitOptions.RemoveEmptyEntries)[1];
            link = link.Substring(0, link.IndexOf(@"""", StringComparison.Ordinal));

            request = WebRequest.Create(link);

            // Get the response.
            try
            {
                response = (HttpWebResponse)await request.GetResponseAsync();
            }
            catch (WebException e)
            {
                if (((HttpWebResponse)e.Response).StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Next page link is broken.  Expected 200, got " + ((HttpWebResponse)e.Response).StatusCode, e);
                }
            }
        }

        private async Task<int> UploadPackageToCuratedFeed(string packageName, string version, FeedType feedType)
        {
            string packageFullPath = string.Empty;
            switch (feedType)
            {
                case FeedType.Windows8CuratedFeed:
                    packageFullPath = await _packageCreationHelper.CreateWindows8CuratedPackage(packageName, version);
                    break;
                case FeedType.WebMatrixCuratedFeed:
                    packageFullPath = await _packageCreationHelper.CreateWindows8CuratedPackage(packageName, version);
                    break;
            }
            var processResult = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            return processResult.ExitCode;
        }

        private string GetCuratedFeedUrl(FeedType type)
        {
            string url = string.Empty;
            switch (type)
            {
                case FeedType.Windows8CuratedFeed:
                    url = UrlHelper.Windows8CuratedFeedUrl;
                    break;
                case FeedType.WebMatrixCuratedFeed:
                    url = UrlHelper.WebMatrixCuratedFeedUrl;
                    break;
                case FeedType.DotnetCuratedFeed:
                    url = UrlHelper.DotnetCuratedFeedUrl;
                    break;
            }
            return url;
        }

        private bool CheckPackageExistInCuratedFeed(string packageName, FeedType feedType)
        {
            string curatedFeedUrl = GetCuratedFeedUrl(feedType);
            var maxAttempts = 10;
            var interval = 30;
            bool applied = false;

            TestOutputHelper.WriteLine("Starting package verification checks ({0} attempts, interval {1} seconds).", maxAttempts, interval);

            for (int i = 0; i < maxAttempts && applied == false; i++)
            {
                TestOutputHelper.WriteLine("[verification attempt {0}]: Waiting {1} seconds before next check...", i, interval);
                if (i != 0)
                {
                    Thread.Sleep(interval * 1000);
                }
                else
                {
                    Thread.Sleep(5000);
                }

                if (_clientSdkHelper.CheckIfPackageExistsInSource(packageName, curatedFeedUrl))
                {
                    applied = true;
                }
            }
            return applied;
        }

        private async Task CheckCuratedFeedForDuplicates(FeedType feedType)
        {
            var request = WebRequest.Create(GetCuratedFeedUrl(feedType) + "Packages");
            request.Timeout = 15000;
            ArrayList packages = new ArrayList();

            // Get the response.
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            responseText = responseText.Substring(responseText.IndexOf("<entry>", StringComparison.Ordinal));
            CheckPageForDuplicates(packages, responseText);

            while (responseText.Contains(@"<link rel=""next"" href="""))
            {
                // Get the link to the next page.
                string link = responseText.Split(new[] { @"<link rel=""next"" href=""" }, StringSplitOptions.RemoveEmptyEntries)[1];
                link = link.Substring(0, link.IndexOf(@"""", StringComparison.Ordinal));

                request = WebRequest.Create(link);
                request.Timeout = 2000;

                // Get the response.
                try
                {
                    response = (HttpWebResponse)await request.GetResponseAsync();
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        responseText = await sr.ReadToEndAsync();
                    }

                    responseText = responseText.Substring(responseText.IndexOf("<entry>", StringComparison.Ordinal));
                    CheckPageForDuplicates(packages, responseText);
                }
                catch (WebException e)
                {
                    if (((HttpWebResponse)e.Response).StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("Next page link is broken.  Expected 200, got " + ((HttpWebResponse)e.Response).StatusCode, e);
                    }
                }
            }
        }

        private static void CheckPageForDuplicates(ArrayList packages, string responseText)
        {
            string unreadPortion = responseText;

            while (unreadPortion.Contains("<id>"))
            {
                unreadPortion = unreadPortion.Substring(unreadPortion.IndexOf("<id>", StringComparison.Ordinal) + 4);
                string packageIdString = unreadPortion.Substring(0, unreadPortion.IndexOf("</id>", StringComparison.Ordinal));
                if (packages.Contains(packageIdString))
                {
                    throw new Exception("A package appeared twice in the WebMatrix feed: " + packageIdString);
                }
                else
                {
                    packages.Add(packageIdString);
                }
                unreadPortion = unreadPortion.Substring(1);
            }
        }
    }
}
