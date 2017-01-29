// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
            await _clientSdkHelper.UploadNewPackage(packageId);

            TestOutputHelper.WriteLine("Uploaded package '{0}'", packageId);
            await _clientSdkHelper.UploadNewPackage(packageId, "2.0.0");

            // "&$orderby=Version" is appended to bypass the search hijacker
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
            var startingTime = DateTime.UtcNow;

            // Upload the packages in order.
            var uploadStartTimestamp = DateTime.UtcNow.AddMinutes(-1);
            for (var i = 0; i < PackagesInOrderNumPackages; i++)
            {
                var packageId = GetPackagesAppearInFeedInOrderPackageId(startingTime, i);
                await _clientSdkHelper.UploadNewPackage(packageId);
                packageIds.Add(packageId);
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

        private static string GetPackagesAppearInFeedInOrderPackageId(DateTime startingTime, int i)
        {
            return $"TestV2FeedPackagesAppearInFeedInOrderTest.{startingTime.Ticks}.{i}";
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
            // Use the same package name, but force the version to be unique.
            var packageName = "NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest";
            var ticks = DateTime.Now.Ticks.ToString();
            var version1 = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            var version2 = new Version(Convert.ToInt32(ticks.Substring(0, 6) + 1) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            var package1Location = await _packageCreationHelper.CreatePackageWithTargetFramework(packageName, version1, "net45");

            var processResult = await _commandlineHelper.UploadPackageAsync(package1Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 0, Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            var package2Location = await _packageCreationHelper.CreatePackageWithTargetFramework(packageName, version2, "net40");
            processResult = await _commandlineHelper.UploadPackageAsync(package2Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.True((processResult.ExitCode == 0), Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            var url = UrlHelper.V2FeedRootUrl + @"/GetUpdates()?packageIds='NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest%7COwin%7CMicrosoft.Web.Infrastructure%7CMicrosoft.AspNet.Identity.Core%7CMicrosoft.AspNet.Identity.EntityFramework%7CMicrosoft.AspNet.Identity.Owin%7CMicrosoft.AspNet.Web.Optimization%7CRespond%7CWebGrease%7CjQuery%7CjQuery.Validation%7CMicrosoft.Owin.Security.Twitter%7CMicrosoft.Owin.Security.OAuth%7CMicrosoft.Owin.Security.MicrosoftAccount%7CMicrosoft.Owin.Security.Google%7CMicrosoft.Owin.Security.Facebook%7CMicrosoft.Owin.Security.Cookies%7CMicrosoft.Owin%7CMicrosoft.Owin.Host.SystemWeb%7CMicrosoft.Owin.Security%7CModernizr%7CMicrosoft.jQuery.Unobtrusive.Validation%7CMicrosoft.AspNet.WebPages%7CMicrosoft.AspNet.Razor%7Cbootstrap%7CAntlr%7CMicrosoft.AspNet.Mvc%7CNewtonsoft.Json%7CEntityFramework'&versions='" + version1 + "%7C1.0%7C1.0.0.0%7C1.0.0%7C1.0.0%7C1.0.0%7C1.1.1%7C1.2.0%7C1.5.2%7C1.10.2%7C1.11.1%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.6.2%7C3.0.0%7C3.0.0%7C3.0.0%7C3.0.0%7C3.4.1.9004%7C5.0.0%7C5.0.6%7C6.0.0'&includePrerelease=false&includeAllVersions=false&targetFrameworks='net45'&versionConstraints='%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C'";
            string[] expectedTexts =
            {
                @"<title type=""text"">NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest</title>",
                @"<d:Version>" + version2 + "</d:Version><d:NormalizedVersion>" + version2 + "</d:NormalizedVersion>"
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
