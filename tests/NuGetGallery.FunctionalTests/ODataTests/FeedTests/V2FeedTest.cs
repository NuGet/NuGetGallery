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
    public partial class V2FeedTest : GalleryTestBase
    {
        [TestMethod]
        [Description("Verify the webresponse from /Api/V2/ feed contains the Packages text")]
        [Priority(0)]
        public void ApiV2BaseUrlTest()
        {
            string expectedText = @"<atom:title>Packages</atom:title>";
            Assert.IsTrue(ODataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText));
        }

        [TestMethod]
        [Description("Verify the webresponse from /Api/V2/$metadata contains the V2FeedPackage text")]
        [Priority(0)]
        public void ApiV2MetadataTest()
        {
            string expectedText = @"V2FeedPackage";
            Assert.IsTrue(ODataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl + @"$metadata", expectedText));
        }

        [TestMethod]
        [Description("Verify the webresponse from top30 packages feed contains jQuery")]
        [Priority(0)]
        public void Top30PackagesFeedTest()
        {
            string url = UrlHelper.V2FeedRootUrl + @"/Search()?$filter=IsAbsoluteLatestVersion&$orderby=DownloadCount%20desc,Id&$skip=0&$top=30&searchTerm=''&targetFramework='net45'&includePrerelease=true";
            Assert.IsTrue(ODataHelper.ContainsResponseText(url, "jQuery"));
        }

        [TestMethod]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void DownloadPackageFromV2Feed()
        {
            ODataHelper.DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Install");
        }

        [TestMethod]
        [Description("Restores a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void RestorePackageFromV2Feed()
        {
            ODataHelper.DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Restore");
        }
    }
}

