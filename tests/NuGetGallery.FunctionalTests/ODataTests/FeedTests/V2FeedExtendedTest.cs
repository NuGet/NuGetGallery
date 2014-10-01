using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.IO;
using System.Net;

namespace NuGetGallery.FunctionalTests.ODataFeedTests 
{
    [TestClass]
    public partial class V2FeedTest : GalleryTestBase
    {       
        [TestMethod]
        [Description("Upload two packages and then issue the FindPackagesById request, expect to return both versions")]
        [Priority(1)]
        public void FindPackagesByIdTest()
        {
            // Temporary workaround for the SSL issue, which keeps the upload test from working with cloudapp.net sites
            if (UrlHelper.BaseUrl.Contains("nugettest.org") || UrlHelper.BaseUrl.Contains("nuget.org"))
            {
                string packageId = "TestV2FeedFindPackagesById" + "." + DateTime.Now.Ticks.ToString();
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "1.0.0");
                AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "2.0.0");
                string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'";
                string[] expectedTexts = new string[] { @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='1.0.0')</id>", @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='2.0.0')</id>" };
                Assert.IsTrue(ODataHelper.ContainsResponseText(url, expectedTexts));
            }
        }

        /// <summary>
        /// Regression test for #1199, also covers #1052
        /// </summary>
        [TestMethod]
        [Description("GetUpdates test, with updated package version having a different targetframework moniker")]
        [Priority(1)]
        public void GetUpdates1199RegressionTest()
        {
            // Use the same package name, but force the version to be unique.
            string packageName = "NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest";
            string ticks = DateTime.Now.Ticks.ToString();
            string version1 = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string version2 = new System.Version(Convert.ToInt32(ticks.Substring(0, 6) + 1).ToString() + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string package1Location = PackageCreationHelper.CreatePackageWithTargetFramework(packageName, version1, "net45");
            int exitCode = CmdLineHelper.UploadPackage(package1Location, UrlHelper.V2FeedPushSourceUrl, out standardOutput, out standardError);
            Assert.IsTrue((exitCode == 0), Constants.UploadFailureMessage + "Exit Code: " + exitCode + ". Error message: \"" + standardError + "\"");
            string package2Location = PackageCreationHelper.CreatePackageWithTargetFramework(packageName, version2, "net40");
            exitCode = CmdLineHelper.UploadPackage(package2Location, UrlHelper.V2FeedPushSourceUrl, out standardOutput, out standardError);
            Assert.IsTrue((exitCode == 0), Constants.UploadFailureMessage + "Exit Code: " + exitCode + ". Error message: \"" + standardError + "\"");
            
            string url = UrlHelper.V2FeedRootUrl + @"/GetUpdates()?packageIds='NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest%7COwin%7CMicrosoft.Web.Infrastructure%7CMicrosoft.AspNet.Identity.Core%7CMicrosoft.AspNet.Identity.EntityFramework%7CMicrosoft.AspNet.Identity.Owin%7CMicrosoft.AspNet.Web.Optimization%7CRespond%7CWebGrease%7CjQuery%7CjQuery.Validation%7CMicrosoft.Owin.Security.Twitter%7CMicrosoft.Owin.Security.OAuth%7CMicrosoft.Owin.Security.MicrosoftAccount%7CMicrosoft.Owin.Security.Google%7CMicrosoft.Owin.Security.Facebook%7CMicrosoft.Owin.Security.Cookies%7CMicrosoft.Owin%7CMicrosoft.Owin.Host.SystemWeb%7CMicrosoft.Owin.Security%7CModernizr%7CMicrosoft.jQuery.Unobtrusive.Validation%7CMicrosoft.AspNet.WebPages%7CMicrosoft.AspNet.Razor%7Cbootstrap%7CAntlr%7CMicrosoft.AspNet.Mvc%7CNewtonsoft.Json%7CEntityFramework'&versions='" + version1 + "%7C1.0%7C1.0.0.0%7C1.0.0%7C1.0.0%7C1.0.0%7C1.1.1%7C1.2.0%7C1.5.2%7C1.10.2%7C1.11.1%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.0.0%7C2.6.2%7C3.0.0%7C3.0.0%7C3.0.0%7C3.0.0%7C3.4.1.9004%7C5.0.0%7C5.0.6%7C6.0.0'&includePrerelease=false&includeAllVersions=false&targetFrameworks='net45'&versionConstraints='%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C%7C'";
            string[] expectedTexts = new string[] { @"<title type=""text"">NuGetGallery.FunctionalTests.ODataTests.GetUpdates1199RegressionTest</title>", @"<d:Version>" + version2 + "</d:Version><d:NormalizedVersion>" + version2 + "</d:NormalizedVersion>" };
            Assert.IsTrue(ODataHelper.ContainsResponseText(url, expectedTexts));
        }

        /// <summary>
        /// Double-checks whether feed and stats page rankings are the same.
        /// </summary>
        [TestMethod]
        [Description("Verify the most downloaded package list returned by the feed is the same with that shown on the statistics page")]
        [Priority(1)]
        public void PackageFeedSortingTest()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            // Grab the top 10 package names in the feed.
            string[] packageName = new string[10];
            responseText = packageName[0] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """) + 14);
            packageName[0] = packageName[0].Substring(0, responseText.IndexOf(@""""));
            for (int i = 1; i < 10; i++)
            {
                responseText = packageName[i] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """) + 14);
                packageName[i] = packageName[i].Substring(0, responseText.IndexOf(@""""));
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
            response = request.GetResponse();
            sr = new StreamReader(response.GetResponseStream());
            responseText = sr.ReadToEnd();
            for (int i = 1; i < 10; i++)
            {
                // Check to make sure the top 10 packages are in the same order as the feed.
                // We add angle brackets to prevent false failures due to duplicate package names in the page.
                Assert.IsTrue(responseText.IndexOf(">" + packageName[i - 1] + "<") < responseText.IndexOf(">" + packageName[i] + "<"), "Expected string " + packageName[i - 1] + " to come before " + packageName[i] + ".  Expected list is: " + packageName[0] + ", " + packageName[1] + ", " + packageName[2] + ", " + packageName[3] + ", " + packageName[4] + ", " + packageName[5] + ", " + packageName[6] + ", " + packageName[7] + ", " + packageName[8] + ", " + packageName[9]);
            }
        }
    }
}
