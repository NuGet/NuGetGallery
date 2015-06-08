using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.ODataFeedTests
{
    [TestClass]
    public partial class V2FeedTest
        : GalleryTestBase
    {
        [TestMethod]
        [Description("Upload two packages and then issue the FindPackagesById request, expect to return both versions")]
        [Priority(1)]
        public async Task FindPackagesByIdTest()
        {
            // Temporary workaround for the SSL issue, which keeps the upload test from working with cloudapp.net sites
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageId = "TestV2FeedFindPackagesById" + "." + DateTime.UtcNow.Ticks;

                await AssertAndValidationHelper.UploadNewPackageAndVerify(packageId);
                await AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "2.0.0");

                string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'";
                string[] expectedTexts =
                {
                    @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='1.0.0')</id>",
                    @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='2.0.0')</id>"
                };
                var containsResponseText = await ODataHelper.ContainsResponseText(url, expectedTexts);
                Assert.IsTrue(containsResponseText);
            }
        }

        /// <summary>
        /// Regression test for #1199, also covers #1052
        /// </summary>
        [TestMethod]
        [Description("GetUpdates test, with updated package version having a different targetframework moniker")]
        [Priority(1)]
        public async Task GetUpdates1199RegressionTest()
        {
            // Use the same package name, but force the version to be unique.
            string packageName = "NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest";
            string ticks = DateTime.Now.Ticks.ToString();
            string version1 = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string version2 = new Version(Convert.ToInt32(ticks.Substring(0, 6) + 1) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string package1Location = await PackageCreationHelper.CreatePackageWithTargetFramework(packageName, version1, "net45");

            CmdLineHelper.ProcessResult processResult = await CmdLineHelper.UploadPackageAsync(package1Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.IsTrue(processResult.ExitCode == 0, Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");
            string package2Location = await PackageCreationHelper.CreatePackageWithTargetFramework(packageName, version2, "net40");

            processResult = await CmdLineHelper.UploadPackageAsync(package2Location, UrlHelper.V2FeedPushSourceUrl);

            Assert.IsTrue((processResult.ExitCode == 0), Constants.UploadFailureMessage + "Exit Code: " + processResult.ExitCode + ". Error message: \"" + processResult.StandardError + "\"");

            string url = UrlHelper.V2FeedRootUrl + @"/GetUpdates()?packageIds='NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest%7COwin%7CMicrosoft.Web.Infrastructure%7CMicrosoft.AspNet.Identity.Core%7CMicrosoft.AspNet.Identity.EntityFramework%7CMicrosoft.AspNet.Identity.Owin%7CMicrosoft.AspNet.Web.Optimization%7CRespond%7CWebGrease%7CjQuery%7CjQuery.Validation%7CMicrosoft.Owin.Security.Twitter%7CMicrosoft.Owin.Security.OAuth%7CMicrosoft.Owin.Security.MicrosoftAccount%7CMicrosoft.Owin.Security.Google%7CMicrosoft.Owin.Security.Facebook%7CMicrosoft.Owin.Security.Cookies%7CMicrosoft.Owin%7CMicrosoft.Owin.Host.SystemWeb%7CMicrosoft.Owin.Security%7CModernizr%7CMicrosoft.jQuery.Unobtrusive.Validation%7CMicrosoft.AspNet.WebPages%7CMicrosoft.AspNet.Razor%7Cbootstrap%7CAntlr%7CMicrosoft.AspNet.Mvc%7CNewtonsoft.Json%7CEntityFramework'&versions='" + version1 + "%7C1.0%7C1.0.0.0%7C1.0.0%7C1.0.0%7C1.0.0%7C1.1.1%7C1.2.0%7C1.5.2%7C1.10.2%7C1.11.1%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.6.2%7C3.0.0%7C3.0.0%7C3.0.0%7C3.0.0%7C3.4.1.9004%7C5.0.0%7C5.0.6%7C6.0.0'&includePrerelease=false&includeAllVersions=false&targetFrameworks='net45'&versionConstraints='%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C'";
            string[] expectedTexts =
            {
                @"<title type=""text"">NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest</title>",
                @"<d:Version>" + version2 + "</d:Version><d:NormalizedVersion>" + version2 + "</d:NormalizedVersion>"
            };
            var containsResponseText = await ODataHelper.ContainsResponseText(url, expectedTexts);

            Assert.IsTrue(containsResponseText);
        }

        /// <summary>
        /// Double-checks whether feed and stats page rankings are the same.
        /// </summary>
        [TestMethod]
        [Description("Verify the most downloaded package list returned by the feed is the same with that shown on the statistics page")]
        [Priority(1)]
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
                Assert.IsTrue(condition, "Expected string " + packageName[i - 1] + " to come before " + packageName[i] + ".  Expected list is: " + packageName[0] + ", " + packageName[1] + ", " + packageName[2] + ", " + packageName[3] + ", " + packageName[4] + ", " + packageName[5] + ", " + packageName[6] + ", " + packageName[7] + ", " + packageName[8] + ", " + packageName[9]);
            }
        }
    }
}
