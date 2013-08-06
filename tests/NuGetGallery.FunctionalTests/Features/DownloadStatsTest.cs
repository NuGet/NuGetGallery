using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System.IO;
using NuGetGallery.FunctionalTests.TestBase;


namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class DownloadStatsTest : GalleryTestBase
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
        [Description("Uploads and downloads multiple versions of a package and validates the download count.")]
        [Priority(1)]
        [Ignore] // Nuget.Core returns the downloadcount for package registration always and doesnt return the count for package versions. Ignoring it for now.
        public void DownloadStatsForPackageVersions()
        {
            string packageId = testContextInstance.TestName + DateTime.Now.Ticks.ToString();
            //Upload package.
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId,"1.0.0");
            //Upload 2.0 for the same package.
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId,"2.0.0");

            //check download count for the package registration and package versions.
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatistics(packageId).Equals(0), "Package download count is not zero as soon as uploading. Actual value : {0}", ClientSDKHelper.GetDownLoadStatistics(packageId));
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "1.0.0").Equals(0), "Package version download count is not zero as soon as uploading. Actual value : {0}", ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "1.0.0"));
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "2.0.0").Equals(0), "Package version download count is not zero as soon as uploading. Actual value : {0}", ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "2.0.0"));

            //Download the new package.
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId, "1.0.0");
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId, "2.0.0");

            //Wait for a max of 5 mins ( as the stats job runs every 5 mins).
            int downloadCount = ClientSDKHelper.GetDownLoadStatistics(packageId);
            int waittime = 0;
            while (downloadCount == 0 && waittime <= 300)
            {
                downloadCount = ClientSDKHelper.GetDownLoadStatistics(packageId);
                System.Threading.Thread.Sleep(30 * 1000);//sleep for 30 seconds.
                waittime += 30;
            }
            //check download count. Download count for the package registration should be 2 and the download count for the package versions should be 1.
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatistics(packageId) == 2, "Package download count is not increased after downloading a new package. Actual value : {0}", ClientSDKHelper.GetDownLoadStatistics(packageId));
            //TBD : Seems like NuGet.Core returns the download count for package registration always.
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "1.0.0") == 1, "Package version download count is not 1 after downloading a new package. Actual value : {0}", ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "1.0.0"));
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "2.0.0") == 1, "Package version download count is not 1 after downloading a new package. Actual value : {0}", ClientSDKHelper.GetDownLoadStatisticsForPackageVersion(packageId, "2.0.0"));

        }
    }
}
