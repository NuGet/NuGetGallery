using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class CuratedFeedTest
    {
        [TestMethod]
        [Ignore]
        [Description("Creates a package with Windows8 tags. Uploads it and checks if it has been curated")]
        public void AddPackageToWindows8CuratedFeed()
        {
            string packageId = DateTime.Now.Ticks.ToString();
            string packageFullPath = CmdLineHelper.CreateWindows8CuratedPackage(packageId);
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedRootUrl + "package/");
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
            //check if the package is present in windows 8 feed.
            //TBD : Need to check the exact the url for curated feed.
             Assert.IsTrue(ClientSDKHelper.CheckIfPackageExistsInSource(packageId, UrlHelper.Windows8CuratedFeedUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.V2FeedRootUrl);
        }
    }
}
