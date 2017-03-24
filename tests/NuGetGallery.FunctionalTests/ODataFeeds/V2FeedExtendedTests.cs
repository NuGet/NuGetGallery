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

        [Fact]
        [Description("Verifies that the IsLatest/IsLatestStable flags are set correctly when different package versions are pushed concurrently")]
        [Priority(1)]
        [Category("P0Tests")]
        public async Task PackageLatestSetCorrectlyOnConcurrentPushes()
        {
            var latestAll = "7.0.2-abc";
            var latestStableAll = "7.0.1";
            var latestFirstHalf = "3.0.2-abc";
            var latestStableFirstHalf = "3.0.1";

            var packageId = $"TestV2FeedPackageLatestSetCorrectlyOnConcurrentPushes.{DateTime.UtcNow.Ticks}";
            var packageVersions = new List<string>()
            {
                 "1.0.0-a",  "1.0.0-b",  "1.0.0",  "1.0.1",  "1.0.2-abc",
                 "2.0.0-a",  "2.0.0-b",  "2.0.0",  "2.0.1",  "2.0.2-abc",
                 "3.0.0-a",  "3.0.0-b",  "3.0.0",  "3.0.1",  "3.0.2-abc",
                 "4.0.0-a",  "4.0.0-b",  "4.0.0",  "4.0.1",  "4.0.2-abc",
                 "6.0.0-a",  "6.0.0-b",  "6.0.0",  "6.0.1",  "6.0.2-abc",
                 "7.0.0-a",  "7.0.0-b",  "7.0.0",  "7.0.1",  "7.0.2-abc"
            };

            // push all and verify; ~15-20 concurrency conflicts seen in testing
            var concurrentTasks = new Task[packageVersions.Count];
            for (int i = 0; i < concurrentTasks.Length; i++)
            {
                var packageVersion = packageVersions[i];
                concurrentTasks[i] = Task.Run(() => _clientSdkHelper.UploadNewPackage(packageId, version: packageVersion));
            }
            Task.WaitAll(concurrentTasks);
            
            await CheckPackageLatestVersions(packageId, packageVersions, latestAll, latestStableAll);

            // unlist last half and verify; ~1-2 concurrency conflicts seen in testing
            for (int i = concurrentTasks.Length - 1; i >= 15; i--)
            {
                var packageVersion = packageVersions[i];
                concurrentTasks[i] = Task.Run(() => _clientSdkHelper.UnlistPackage(packageId, version: packageVersion));
            }
            Task.WaitAll(concurrentTasks);

            await CheckPackageLatestVersions(packageId, packageVersions, latestFirstHalf, latestStableFirstHalf);

            // unlist remaining and verify; ~1-2 concurrency conflicts seen in testing
            for (int i = 14; i >= 0; i--)
            {
                var packageVersion = packageVersions[i];
                concurrentTasks[i] = Task.Run(() => _clientSdkHelper.UnlistPackage(packageId, version: packageVersion));
            }
            Task.WaitAll(concurrentTasks);

            await CheckPackageLatestVersions(packageId, packageVersions, string.Empty, string.Empty);
        }

        private static string GetPackagesAppearInFeedInOrderPackageId(DateTime startingTime, int i)
        {
            return $"TestV2FeedPackagesAppearInFeedInOrderTest.{startingTime.Ticks}.{i}";
        }

        private static string GetPackagesAppearInFeedInOrderUrl(DateTime time, string timestamp)
        {
            return $"{UrlHelper.V2FeedRootUrl}/Packages?$filter={timestamp} gt DateTime'{time:o}'&$orderby={timestamp} desc&$select={timestamp},IsLatestVersion,IsAbsoluteLatestVersion,Published";
        }

        /// <summary>
        /// Verifies if a set of packages in the feed have timestamps in a particular order.
        /// </summary>
        /// <param name="packageIds">An ordered list of package ids. Each package id in the list must have a timestamp in the feed earlier than all package ids after it.</param>
        /// <param name="timestampPropertyName">The timestamp property to test the ordering of. For example, "Created" or "LastEdited".</param>
        /// <param name="operationStartTimestamp">A timestamp that is before all of the timestamps expected to be found in the feed. This is used in a request to the feed.</param>
        /// <param name="packagesListed">Whether packages are listed, used to verify if latest flags are set properly.</param>
        private async Task CheckPackageTimestampsInOrder(List<string> packageIds, string timestampPropertyName,
            DateTime operationStartTimestamp)
        {
            var lastTimestamp = DateTime.MinValue;
            for (var i = 0; i < PackagesInOrderNumPackages; i++)
            {
                var packageId = packageIds[i];
                TestOutputHelper.WriteLine($"Attempting to check order of package #{i} {timestampPropertyName} timestamp in feed.");

                var feedUrl = GetPackagesAppearInFeedInOrderUrl(operationStartTimestamp, timestampPropertyName);
                var packageDetails = await _odataHelper.GetPackagePropertiesFromResponse(feedUrl, packageId);
                Assert.NotNull(packageDetails);
                
                var newTimestamp = (DateTime?)(packageDetails.ContainsKey(timestampPropertyName)
                    ? packageDetails[timestampPropertyName]
                    : null);

                Assert.True(newTimestamp.HasValue);
                Assert.True(newTimestamp.Value > lastTimestamp,
                    $"Package #{i} was last modified after package #{i - 1} but has an earlier {timestampPropertyName} timestamp ({newTimestamp} should be greater than {lastTimestamp}).");
                lastTimestamp = newTimestamp.Value;

                var published= packageDetails["Published"] as DateTime?;
                var isListed = !published?.Year.Equals(1900);
                var isLatest = packageDetails["IsAbsoluteLatestVersion"] as bool?;
                var isLatestStable = packageDetails["IsLatestVersion"] as bool?;

                Assert.NotNull(isLatest);
                Assert.Equal(isLatest, isListed);

                Assert.NotNull(isLatestStable);
                Assert.Equal(isLatestStable, isListed);
            }
        }
        
        private async Task CheckPackageLatestVersions(string packageId, List<string> versions, string expectedLatest, string expectedLatestStable)
        {
            foreach (var version in versions)
            {
                var feedUrl = $"{UrlHelper.V2FeedRootUrl}/Packages?$filter=Id eq '{packageId}' and Version eq '{version}'" +
                    "&$select=Id,Version,IsLatestVersion,IsAbsoluteLatestVersion";
                var packageDetails = await _odataHelper.GetPackagePropertiesFromResponse(feedUrl, packageId, version);
                Assert.NotNull(packageDetails);

                var actualId = packageDetails["Id"] as string;
                Assert.True(packageId.Equals(actualId, StringComparison.InvariantCultureIgnoreCase));

                var actualVersion = packageDetails["Version"] as string;
                Assert.True(version.Equals(actualVersion, StringComparison.InvariantCultureIgnoreCase));

                var isLatest = packageDetails["IsAbsoluteLatestVersion"] as bool?;
                Assert.NotNull(isLatest);
                Assert.Equal(isLatest, expectedLatest.Equals(version, StringComparison.InvariantCultureIgnoreCase));

                var isLatestStable = packageDetails["IsLatestVersion"] as bool?;
                Assert.NotNull(isLatestStable);
                Assert.Equal(isLatestStable, expectedLatestStable.Equals(version, StringComparison.InvariantCultureIgnoreCase));
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
            var packageName = "GetUpdates1199RegressionTest";
            var ticks = DateTime.Now.Ticks.ToString();
            var version1 = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            var version2 = new Version(Convert.ToInt32(ticks.Substring(0, 6) + 1) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            var package1Location = await _packageCreationHelper.CreatePackageWithTargetFramework(packageName, version1, "net45");

            var processResult = await _commandlineHelper.UploadPackageAsync(package1Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.True(processResult.ExitCode == 0, Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            var package2Location = await _packageCreationHelper.CreatePackageWithTargetFramework(packageName, version2, "net40");
            processResult = await _commandlineHelper.UploadPackageAsync(package2Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.True((processResult.ExitCode == 0), Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            var packagesList = new List<string>
            {
                packageName,
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
                $@"<title type=""text"">{packageName}</title>",
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

        [Fact]
        [Description("VerifyPackageKey fails if package isn't found.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns404ForMissingPackage()
        {
            Assert.Equal(HttpStatusCode.NotFound, await VerifyPackageKey(EnvironmentSettings.TestAccountApiKey, "VerifyPackageKeyReturns404ForMissingPackage", "1.0.0"));
        }

        [Fact]
        [Description("VerifyPackageKey succeeds for full API key without deletion.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns200ForFullApiKey()
        {
            var packageId = $"VerifyPackageKeyReturns200ForFullApiKey.{DateTimeOffset.UtcNow.Ticks}";
            var packageVersion = "1.0.0";

            await _clientSdkHelper.UploadNewPackage(packageId, packageVersion);
            
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(EnvironmentSettings.TestAccountApiKey, packageId));
            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(EnvironmentSettings.TestAccountApiKey, packageId, packageVersion));
        }

        [Fact]
        [Description("VerifyPackageKey succeeds for temp API key with deletion.")]
        [Priority(1)]
        [Category("P1Tests")]
        public async Task VerifyPackageKeyReturns200ForTempApiKey()
        {
            var packageId = $"VerifyPackageKeySupportsFullAndTempApiKeys.{DateTimeOffset.UtcNow.Ticks}";
            var packageVersion = "1.0.0";

            await _clientSdkHelper.UploadNewPackage(packageId, packageVersion);
            
            var verificationKey = await CreateVerificationKey(packageId, packageVersion);

            Assert.Equal(HttpStatusCode.OK, await VerifyPackageKey(verificationKey, packageId, packageVersion));
            Assert.Equal(HttpStatusCode.Forbidden, await VerifyPackageKey(verificationKey, packageId, packageVersion));
        }

        private async Task<string> CreateVerificationKey(string packageId, string packageVersion)
        {
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + $"package/create-verification-key/{packageId}/{packageVersion}");
            request.Method = "POST";
            request.ContentLength = 0;
            request.Headers.Add("X-NuGet-ApiKey", EnvironmentSettings.TestAccountApiKey);
            request.Headers.Add("X-NuGet-Client-Version", "NuGetGallery.FunctionalTests");

            var response = await request.GetResponseAsync() as HttpWebResponse;
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            var json = JObject.Parse(responseText);
            var expiration = json.Value<DateTime>("Expires");
            Assert.True(expiration - DateTime.UtcNow < TimeSpan.FromDays(1), "Verification keys should expire after 1 day.");

            return json.Value<string>("Key");
        }

        private async Task<HttpStatusCode> VerifyPackageKey(string apiKey, string packageId, string packageVersion = null)
        {
            var route = string.IsNullOrWhiteSpace(packageVersion) ?
                $"verifykey/{packageId}" :
                $"verifykey/{packageId}/{packageVersion}";

            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + route);
            request.Headers.Add("X-NuGet-ApiKey", apiKey);
            
            try
            {
                var response = await request.GetResponseAsync() as HttpWebResponse;
                return response.StatusCode;
            }
            catch (WebException e)
            {
                return ((HttpWebResponse)e.Response).StatusCode;
            }
        }
    }
}
