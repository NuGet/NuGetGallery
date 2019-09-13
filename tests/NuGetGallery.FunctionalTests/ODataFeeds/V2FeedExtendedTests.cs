// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.ODataFeeds
{
    public class V2FeedExtendedTests
        : GalleryTestBase
    {
        private readonly ClientSdkHelper _clientSdkHelper;
        private readonly CommandlineHelper _commandlineHelper;
        private readonly ODataHelper _odataHelper;
        private readonly PackageCreationHelper _packageCreationHelper;

        public V2FeedExtendedTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _clientSdkHelper = new ClientSdkHelper(TestOutputHelper);
            _commandlineHelper = new CommandlineHelper(TestOutputHelper);
            _odataHelper = new ODataHelper(TestOutputHelper);
            _packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
        }

        [Fact]
        [Description("Upload two packages and then issue the FindPackagesById request, expect to return both versions")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task FindPackagesByIdTest()
        {
            var packageInfo = await _clientSdkHelper.UploadPackageVersion();

            var packageId = packageInfo.Id;
            var packageVersion = packageInfo.Version;
            string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'&$orderby=Version";
            var containsResponseText = await _odataHelper.ContainsResponseText(url, @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='" + packageVersion + "')</id>");
            Assert.True(containsResponseText);
        }

        private const int PackagesInOrderNumPackages = 10;

        [Fact]
        [Description("Upload multiple packages and then unlist them and verify that they appear in the feed in the correct order")]
        [Priority(1)]
        [Category("P0Tests")] 
        public async Task PackagesAppearInFeedInOrderTest()
        {
            // This test uploads/unlists packages in a particular order to test the timestamps of the packages in the feed.
            // Because it waits for previous requests to finish before starting new ones, it will only catch ordering issues if these issues are greater than a second or two.
            // This is consistent with the time frame in which we've seen these issues in the past, but if new issues arise that are on a smaller scale, this test will not catch it!
            var uploadedPackageIds = new List<string>();
            var version = "1.0.0";
            var startingTime = DateTime.UtcNow;

            // Upload the packages in order.
            var uploadStartTimestamp = DateTime.UtcNow.AddMinutes(-1);
            for (var i = 0; i < PackagesInOrderNumPackages; i++)
            {
                var packageId = UploadHelper.GetUniquePackageId();
                var packageFullPath = await _packageCreationHelper.CreatePackage(packageId, version);
                var commandOutput = await _commandlineHelper.UploadPackageAsync(packageFullPath, UrlHelper.V2FeedPushSourceUrl);

                Assert.True(
                    commandOutput.ExitCode == 0,
                    $"Push failed with exit code {commandOutput.ExitCode}{Environment.NewLine}{commandOutput.StandardError}");

                uploadedPackageIds.Add(packageId);
            }

            await Task.WhenAll(uploadedPackageIds.Select(id => _clientSdkHelper.VerifyPackageExistsInV2Async(id, version, listed: true)));
            await CheckPackageTimestampsInOrder(uploadedPackageIds, "Created", uploadStartTimestamp, version);

            // Unlist the packages in order.
            var unlistedPackageIds = new List<string>();
            var unlistStartTimestamp = DateTime.UtcNow.AddMinutes(-1);
            foreach (var uploadedPackageId in uploadedPackageIds)
            {
                await _commandlineHelper.DeletePackageAsync(uploadedPackageId, version, UrlHelper.V2FeedPushSourceUrl);
                unlistedPackageIds.Add(uploadedPackageId);
            }

            await Task.WhenAll(unlistedPackageIds.Select(id => _clientSdkHelper.VerifyPackageExistsInV2Async(id, version, listed: false)));
            await CheckPackageTimestampsInOrder(unlistedPackageIds, "LastEdited", unlistStartTimestamp, version);
        }

        private static string GetPackagesAppearInFeedInOrderUrl(DateTime time, string timestamp)
        {
            return $"{UrlHelper.V2FeedRootUrl}/Packages?$filter={timestamp} gt DateTime'{time:o}'&$orderby={timestamp} desc&$select={timestamp}";
        }

        /// <summary>
        /// Verifies if a set of packages in the feed have timestamps in a particular order.
        /// </summary>
        /// <param name="packageIds">An ordered list of package ids. Each package id in the list must have a timestamp in the feed earlier than all package ids after it.</param>
        /// <param name="timestampPropertyName">The timestamp property to test the ordering of. For example, "Created" or "LastEdited".</param>
        /// <param name="operationStartTimestamp">A timestamp that is before all of the timestamps expected to be found in the feed. This is used in a request to the feed.</param>
        private async Task CheckPackageTimestampsInOrder(
            IEnumerable<string> packageIds,
            string timestampPropertyName,
            DateTime operationStartTimestamp,
            string version)
        {
            var lastTimestamp = DateTime.MinValue;
            var lastPackageId = string.Empty;
            foreach (var packageId in packageIds)
            {
                TestOutputHelper.WriteLine($"Attempting to check order of package {packageId} {version} {timestampPropertyName} timestamp in feed.");

                var newTimestamp = await _odataHelper.GetTimestampOfPackageFromResponse(
                    packageId,
                    version,
                    timestampPropertyName);

                Assert.True(newTimestamp.HasValue);
                Assert.True(newTimestamp.Value >= lastTimestamp,
                    $"Package {packageId} was last modified after package {lastPackageId} but has an earlier {timestampPropertyName} timestamp ({newTimestamp} should be greater than {lastTimestamp}).");
                lastTimestamp = newTimestamp.Value;
                lastPackageId = packageId;
            }
        }

        /// <summary>
        /// Regression test for #1199, also covers #1052
        /// </summary>
        [Fact]
        [Description("GetUpdates test, with updated package version having a different targetframework moniker")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task GetUpdates1199RegressionTest()
        {
            // Use unique version to make the assertions simpler.
            var packageId = $"GetUpdates1199RegressionTest.{Guid.NewGuid():N}";
            var version1 = "1.0.0";
            var ticks = DateTime.Now.Ticks.ToString();
            var version2 = new Version(Convert.ToInt32(ticks.Substring(0, 6) + 1) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            var package1Location = await _packageCreationHelper.CreatePackageWithTargetFramework(packageId, version1, "net45");

            var processResult = await _commandlineHelper.UploadPackageAsync(package1Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 0, Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            var package2Location = await _packageCreationHelper.CreatePackageWithTargetFramework(packageId, version2, "net40");
            processResult = await _commandlineHelper.UploadPackageAsync(package2Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.True((processResult.ExitCode == 0), Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            // Wait for the packages to be available in V2 (due to async validation)
            await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, version1);
            await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, version2);

            var packagesList = new List<string>
            {
                packageId,
                "Microsoft.Bcl.Build",
                "Microsoft.Bcl",
                "Microsoft.Net.Http"
            };
            var versionsList = new List<string>
            {
                version1,
                "1.0.6",
                "1.0.19",
                "2.1.6-rc"
            };
            var url = UrlHelper.V2FeedRootUrl +
                @"/GetUpdates()?packageIds='" +
                string.Join("%7C", packagesList) +
                @"'&versions='" +
                string.Join("%7C", versionsList) +
                @"'&includePrerelease=false&includeAllVersions=false&targetFrameworks='net45'&versionConstraints='" +
                string.Join("%7C", Enumerable.Repeat(string.Empty, packagesList.Count)) +
                @"'";
            string[] expectedTexts =
            {
                $@"<title type=""text"">{packageId}</title>",
                $@"<d:Version>{version2}</d:Version><d:NormalizedVersion>{version2}</d:NormalizedVersion>"
            };
            var containsResponseText = await _odataHelper.ContainsResponseText(url, expectedTexts);

            Assert.True(containsResponseText);
        }

        /// <summary>
        /// Double-checks whether feed and stats page rankings are the same.
        /// </summary>
        [Fact]
        [Description("Verify the most downloaded package list returned by the feed is the same with that shown on the statistics page")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task PackageFeedSortingTest()
        {
            var topDownloadsResponse = await GetResponseTextAsync(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");

            // Search Gallery v2 feed for the top 10 unique package ids, or as many as exist.
            string[] topDownloadsNames = GetPackageNamesFromFeedResponse(topDownloadsResponse);

            // Ensure at least 1 package was found.
            Assert.NotEmpty(topDownloadsNames);

            // Search Gallery statistics for the top downloaded packages.
            var statsResponse = await GetResponseTextAsync(UrlHelper.BaseUrl + @"stats/packageversions");

            var expectedNames = String.Join(", ", topDownloadsNames);
            var last = topDownloadsNames.First();
            foreach (var current in topDownloadsNames.Skip(1))
            {
                // Check to make sure the top 10 packages are in the same order as the feed.
                // We add angle brackets to prevent false failures due to duplicate package names in the page.
                var condition = statsResponse.IndexOf(">" + last + "<", StringComparison.Ordinal)
                    < statsResponse.IndexOf(">" + current + "<", StringComparison.Ordinal);
                Assert.True(condition, $"Expected string {last} to come before {current}.  Expected list is: {expectedNames}.");

                Assert.NotEqual(last, current);
                last = current;
            }
        }

        private async Task<string> GetResponseTextAsync(string url)
        {
            using (var httpClient = new HttpClient())
            {
                return await httpClient.GetStringAsync(url);
            }
        }

        private string[] GetPackageNamesFromFeedResponse(string feedResponseText)
        {
            const string PackageIdStartKey = @"""PackageId"": """;
            const string PackageIdEndKey = @"""";

            var results = new List<string>();

            Func<string, int> seekStart = s => s.IndexOf(PackageIdStartKey, StringComparison.Ordinal);
            Func<string, int> seekEnd = s => s.IndexOf(PackageIdEndKey, StringComparison.Ordinal);

            do
            {
                var start = seekStart(feedResponseText);
                if (start < 0)
                {
                    break;
                }
                
                feedResponseText = feedResponseText.Substring(start + PackageIdStartKey.Length);
                var end = seekEnd(feedResponseText);
                if (end >= 0)
                {
                    var name = feedResponseText.Substring(0, end);
                    if (!results.Contains(name, StringComparer.Ordinal))
                    {
                        results.Add(name);
                    }
                    feedResponseText = feedResponseText.Substring(end);
                }
            }
            while (results.Count < 10);

            return results.ToArray();
        }
    }
}
