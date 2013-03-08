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
        [TestMethod]
        [Description("Uploads a test package and downloads it and checks if the download stats has increased appropriately.")]
        [Priority(1)]
        public void DownloadStatsForNewlyUploadedPackage()
        {
            string packageId = DateTime.Now.Ticks.ToString();
            //Upload package
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId);
            //check download count.
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatistics(packageId).Equals(0), "Package download count is not zero as soon as uploading. Actual value : {0}", ClientSDKHelper.GetDownLoadStatistics(packageId));
            //Download the new package.
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
            //Wait for a max of 5 mins ( as the stats job runs every 5 mins).
            int downloadCount = ClientSDKHelper.GetDownLoadStatistics(packageId);
            int waittime = 0;
            while (downloadCount == 0 && waittime <= 300)
            {
                downloadCount = ClientSDKHelper.GetDownLoadStatistics(packageId);
                System.Threading.Thread.Sleep(30 * 1000);//sleep for 30 seconds.
                waittime += 30;
            }
            //check download count.
            Assert.IsTrue(ClientSDKHelper.GetDownLoadStatistics(packageId) >= 1, "Package download count is not increased after downloading a new package. Actual value : {0}", ClientSDKHelper.GetDownLoadStatistics(packageId));

        }
    }
}
