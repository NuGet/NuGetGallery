using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System.Net;
using System.IO;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class CuratedFeedTest
    {
        private TestContext testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        [Description("Creates a package with Windows8 tags. Uploads it and checks if it has been curated")]
        [Priority(1)]
        public void AddPackageToWindows8CuratedFeed()
        {
             string packageId = testContextInstance.TestName + DateTime.Now.Ticks.ToString();
             string packageFullPath = PackageCreationHelper.CreateWindows8CuratedPackage(packageId);
             int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
             Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
            //check if the package is present in windows 8 feed.
            //TBD : Need to check the exact the url for curated feed.
             System.Threading.Thread.Sleep(60000);
             Assert.IsTrue(ClientSDKHelper.CheckIfPackageExistsInSource(packageId, UrlHelper.Windows8CuratedFeedUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.Windows8CuratedFeedUrl);
        }

        [TestMethod]
        [Description("Performs a querystring-based search of the Windows 8 curated feed.  Confirms expected packages are returned.")]
        public void SearchWindows8CuratedFeed()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"curated-feeds/Windows8-Packages/Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='Unity'&includePrerelease=false");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Assert.IsTrue(responseText.Contains(@"<title type=""text"">Unity</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsTrue(responseText.Contains(@"<content type=""application/zip"" src=""" + UrlHelper.V2FeedRootUrl + "curated-feeds/package/Unity/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsFalse(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);
        }


        [TestMethod]
        [Description("Performs a querystring-based search of the WebMatrix curated feed.  Confirms expected packages are returned.")]
        public void SearchWebMatrixCuratedFeed()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"curated-feeds/webmatrix/Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='asp.net%20web%20helpers'&targetFramework='net40'&includePrerelease=false");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Assert.IsTrue(responseText.Contains(@"<title type=""text"">microsoft-web-helpers</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsTrue(responseText.Contains(@"<content type=""application/zip"" src=""" + UrlHelper.V2FeedRootUrl + "curated-feeds/package/microsoft-web-helpers/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsFalse(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);
       
        }
    }
}
