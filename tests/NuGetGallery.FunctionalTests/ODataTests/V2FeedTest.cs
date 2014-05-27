using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests.ODataFeedTests
{
    /// <summary>
    /// Checks if the basic operations against V2 Feed works fine.
    /// </summary>
    [TestClass]
    public partial class V2FeedTest : GalleryTestBase
    {
        [TestMethod]
        public void ApiV2BaseUrlTest()
        {
            string expectedText = @"<atom:title>Packages</atom:title>";
            Assert.IsTrue(ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText));
        }

        [TestMethod]
        public void ApiV2MetadataTest()
        {
            string expectedText = @"V2FeedPackage";
            Assert.IsTrue(ContainsResponseText(UrlHelper.V2FeedRootUrl + @"$metadata", expectedText));
        }

        [TestMethod]
        public void Top30PackagesFeedTest()
        {
            string url = UrlHelper.V2FeedRootUrl + @"/Search()?$filter=IsAbsoluteLatestVersion&$orderby=DownloadCount%20desc,Id&$skip=0&$top=30&searchTerm=''&targetFramework='net45'&includePrerelease=true";
            Assert.IsTrue(ContainsResponseText(url, "jQuery"));
        }

        [TestMethod]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void DownloadPackageFromV2Feed()
        {
            DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Install");
        }

        [TestMethod]
        [Description("Restores a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void RestorePackageFromV2Feed()
        {
            DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Restore");
        }

        public bool ContainsResponseText(string url, params string[] expectedTexts)
        {
            WebRequest request = WebRequest.Create(url);
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            foreach (string s in expectedTexts)
            {
                if (!responseText.Contains(s))
                {
                    Console.WriteLine("Response text does not contain expected text of " + s);
                    return false;
                }
            }
            return true;
        }

        public void DownloadPackageFromV2FeedWithOperation(string packageId, string version, string operation)
        {
            try
            {
                Task<string> downloadTask = ODataHelper.DownloadPackageFromFeed(packageId, version, operation);
                string filename = downloadTask.Result;
                //check if the file exists.
                Assert.IsTrue(File.Exists(filename), Constants.PackageDownloadFailureMessage);
                string downloadedPackageId = ClientSDKHelper.GetPackageIdFromNupkgFile(filename);
                //Check that the downloaded Nupkg file is not corrupt and it indeed corresponds to the package which we were trying to download.
                Assert.IsTrue(downloadedPackageId.Equals(packageId), Constants.UnableToZipError);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }
    }
}

