using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

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
             Assert.IsTrue(ClientSDKHelper.CheckIfPackageExistsInSource(packageId, UrlHelper.Windows8CuratedFeedUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.Windows8CuratedFeedUrl);
        }
    }
}
