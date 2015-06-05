using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.ODataFeedTests
{
    /// <summary>
    /// Checks if the basic operations against V2 Feed works fine.
    /// </summary>
    public partial class V2FeedTest
        : GalleryTestBase
    {
        [TestMethod]
        [Description("Verify the webresponse from /Api/V2/ feed contains the Packages text")]
        [Priority(0)]
        public async Task ApiV2BaseUrlTest()
        {
            string expectedText = @"<atom:title>Packages</atom:title>";
            bool containsResponseText = await ODataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse from /Api/V2/$metadata contains the V2FeedPackage text")]
        [Priority(0)]
        public async Task ApiV2MetadataTest()
        {
            string expectedText = @"V2FeedPackage";
            bool containsResponseText = await ODataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl + @"$metadata", expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse from top30 packages feed contains jQuery")]
        [Priority(0)]
        public async Task Top30PackagesFeedTest()
        {
            string url = UrlHelper.V2FeedRootUrl + @"/Search()?$filter=IsAbsoluteLatestVersion&$orderby=DownloadCount%20desc,Id&$skip=0&$top=30&searchTerm=''&targetFramework='net45'&includePrerelease=true";
            bool containsResponseText = await ODataHelper.ContainsResponseText(url, "jQuery");
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public async Task DownloadPackageFromV2Feed()
        {
            await ODataHelper.DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Install");
        }

        [TestMethod]
        [Description("Restores a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public async Task RestorePackageFromV2Feed()
        {
            await ODataHelper.DownloadPackageFromV2FeedWithOperation(Constants.TestPackageId, "1.0.0", "Restore");
        }
    }
}

