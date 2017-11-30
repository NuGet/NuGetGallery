// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        [Category("P0Tests")]
        public async Task FindPackagesByIdTest()
        {
            string packageId = string.Format("TestV2FeedFindPackagesById.{0}", DateTime.UtcNow.Ticks);
            
            TestOutputHelper.WriteLine("Uploading package '{0}'", packageId);
            await _clientSdkHelper.UploadNewPackage(packageId, "1.0.0");

            TestOutputHelper.WriteLine("Uploaded package '{0}'", packageId);
            await _clientSdkHelper.UploadNewPackage(packageId, "2.0.0");

            // Wait for the packages to be available in V2 (due to async validation)
            await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, "1.0.0");
            await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, "2.0.0");

            string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'&$orderby=Version";
            string[] expectedTexts =
            {
                    @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='1.0.0')</id>",
                    @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='2.0.0')</id>"
                };
            var containsResponseText = await _odataHelper.ContainsResponseText(url, expectedTexts);
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
            var packageIds = new List<string>(PackagesInOrderNumPackages);
            var packageVersion = "1.0.0";
            var startingTime = DateTime.UtcNow;

            // Upload the packages in order.
            var uploadStartTimestamp = DateTime.UtcNow.AddMinutes(-1);
            for (var i = 0; i < PackagesInOrderNumPackages; i++)
            {
                var packageId = $"TestV2FeedPackagesAppearInFeedInOrderTest.{startingTime.Ticks}.{i}";
                await _clientSdkHelper.UploadNewPackage(packageId, packageVersion);
                packageIds.Add(packageId);
            }

            // Wait for the packages to be available in V2 (due to async validation)
            foreach (var packageId in packageIds)
            {
                await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, packageVersion);
            }

            await CheckPackageTimestampsInOrder(packageIds, "Created", uploadStartTimestamp);

            // Unlist the packages in order.
            var unlistStartTimestamp = DateTime.UtcNow.AddMinutes(-1);
            for (var i = 0; i < PackagesInOrderNumPackages; i++)
            {
                await _clientSdkHelper.UnlistPackage(packageIds[i]);
            }

            await CheckPackageTimestampsInOrder(packageIds, "LastEdited", unlistStartTimestamp);
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
        private async Task CheckPackageTimestampsInOrder(List<string> packageIds, string timestampPropertyName,
            DateTime operationStartTimestamp)
        {
            var lastTimestamp = DateTime.MinValue;
            for (var i = 0; i < PackagesInOrderNumPackages; i++)
            {
                var packageId = packageIds[i];
                TestOutputHelper.WriteLine($"Attempting to check order of package #{i} {timestampPropertyName} timestamp in feed.");

                var newTimestamp =
                    await
                        _odataHelper.GetTimestampOfPackageFromResponse(
                            GetPackagesAppearInFeedInOrderUrl(operationStartTimestamp, timestampPropertyName),
                            timestampPropertyName,
                            packageId);

                Assert.True(newTimestamp.HasValue);
                Assert.True(newTimestamp.Value > lastTimestamp,
                    $"Package #{i} was last modified after package #{i - 1} but has an earlier {timestampPropertyName} timestamp ({newTimestamp} should be greater than {lastTimestamp}).");
                lastTimestamp = newTimestamp.Value;
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
            var ticks = DateTime.Now.Ticks.ToString();
            var packageId = $"GetUpdates1199RegressionTest.{ticks}";
            var version1 = "1.0.0";
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
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            // Grab the top 10 package names in the feed.
            string[] packageName = new string[10];
            responseText = packageName[0] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """, StringComparison.Ordinal) + 14);
            packageName[0] = packageName[0].Substring(0, responseText.IndexOf(@"""", StringComparison.Ordinal));
            for (int i = 1; i < 10; i++)
            {
                responseText = packageName[i] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """, StringComparison.Ordinal) + 14);
                packageName[i] = packageName[i].Substring(0, responseText.IndexOf(@"""", StringComparison.Ordinal));
                // Sometimes two versions of a single package appear in the top 10.  Stripping second and later instances for this test.
                for (int j = 0; j < i; j++)
                {
                    if (packageName[j] == packageName[i])
                    {
                        packageName[i] = null;
                        i--;
                    }
                }
            }

            request = WebRequest.Create(UrlHelper.BaseUrl + @"stats/packageversions");

            // Get the response.
            response = await request.GetResponseAsync();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            for (int i = 1; i < 10; i++)
            {
                // Check to make sure the top 10 packages are in the same order as the feed.
                // We add angle brackets to prevent false failures due to duplicate package names in the page.
                var condition = responseText.IndexOf(">" + packageName[i - 1] + "<", StringComparison.Ordinal) < responseText.IndexOf(">" + packageName[i] + "<", StringComparison.Ordinal);
                Assert.True(condition, "Expected string " + packageName[i - 1] + " to come before " + packageName[i] + ".  Expected list is: " + packageName[0] + ", " + packageName[1] + ", " + packageName[2] + ", " + packageName[3] + ", " + packageName[4] + ", " + packageName[5] + ", " + packageName[6] + ", " + packageName[7] + ", " + packageName[8] + ", " + packageName[9]);
            }
        }
    }
}
