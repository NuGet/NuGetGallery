using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using System.IO;

namespace NuGetGallery.FunctionalTests.ODataTests 
{
    [TestClass]
    public partial class V2FeedTest : GalleryTestBase
    {
        [TestMethod]
        public void GetUpdatesTest()
        {
        }

        [TestMethod]
        public void FindPackagesByIdTest()
        {
            string packageId = "TestV2FeedFindPackagesById" + "." + DateTime.Now.Ticks.ToString();
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "1.0.0");
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "2.0.0");
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId +"'");          
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            Assert.IsTrue(responseText.Contains(@"<id>"+ UrlHelper.V2FeedRootUrl + "Packages(Id='"+ packageId + "',Version='1.0.0')</id>"));
            Assert.IsTrue(responseText.Contains(@"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='2.0.0')</id>"));
           
        }

        /// <summary>
        ///     Double-checks whether feed and stats page rankings are the same.
        /// </summary>
        [TestMethod]
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

        /// <summary>
        ///     Double-checks whether expected fields exist in the packages feed.
        /// </summary>
        [TestMethod]
        public void PackageFeedSanityTest()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            string firstPackage = responseText.Substring(responseText.IndexOf("{"), responseText.IndexOf("}") - responseText.IndexOf("{"));

            Assert.IsTrue(firstPackage.Contains(@"""PackageId"": """), "Expected PackageId field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""PackageVersion"": """), "Expected PackageVersion field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""Gallery"": """), "Expected Gallery field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""PackageTitle"": """), "Expected PackageTitle field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""PackageIconUrl"": """), "Expected PackageIconUrl field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""Downloads"": "), "Expected PackageIconUrl field is missing.");
        }

        /// <summary>
        ///     Verify copunt querystring parameter in the Packages feed.
        /// </summary>
        [TestMethod]
        public void PackageFeedCountParameterTest()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            string[] separators = new string[1] {"},"};
            int packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(packageCount == 500, "Expected feed to contain 500 packages. Actual count: " + packageCount);

            request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks?count=5");
            // Get the response.          
            response = request.GetResponse();
            sr = new StreamReader(response.GetResponseStream());
            responseText = sr.ReadToEnd();

            packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(packageCount == 5, "Expected feed to contain 5 packages. Actual count: " + packageCount);
        }
    }
}
