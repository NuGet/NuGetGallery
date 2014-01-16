using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System.IO;
using NuGetGallery.FunctionalTests.TestBase;

namespace NuGetGallery.FunctionalTests.ClientIntegrationTests
{
    /// <summary>
    /// Tries to download and upload package from the gallery using NuGet.exe client.
    /// </summary>
    [TestClass]
    public class NugetCommandLineTests : GalleryTestBase
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
        [Description("Downloads a package using NuGet.exe and checks if the package file is present in the output dir")]
        [Priority(0)]
        public void DownPackageWithNuGetCommandLineTest()
        {
           string packageId = Constants.TestPackageId; //try to down load a pre-defined test package.          
           ClientSDKHelper.ClearLocalPackageFolder(packageId);
           int exitCode = CmdLineHelper.InstallPackage(packageId, UrlHelper.V2FeedRootUrl);
           Assert.IsTrue((exitCode == 0), "The package install via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
           Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");

        }

        [TestMethod]
        [Description("Creates a test package and pushes it to the server using Nuget.exe")]
        [Priority(0)]
        public void UploadPackageWithNuGetCommandLineTest()
        {
            AssertAndValidationHelper.UploadNewPackageAndVerify(DateTime.Now.Ticks.ToString());
        }

        [TestMethod]
        [Description("Creates a test package with minclientversion tag and pushes it to the server using Nuget.exe")]
        [Priority(0)]
        public void UploadAndDownLoadPackageWithMinClientVersion()
        {
            string packageId = DateTime.Now.Ticks.ToString() + testContextInstance.TestName;
            string version = "1.0.0";
            string packageFullPath = PackageCreationHelper.CreatePackageWithMinClientVersion(packageId,version, "2.3");         
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.V2FeedRootUrl);

            //Delete package from local disk so once it gets uploaded
            if (File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }
            System.Threading.Thread.Sleep(30000);
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }

        [TestMethod]
        [Description("Creates a test package which ends with .cs and pushes it to the server using Nuget.exe")]
        [Priority(1)]
        public void UploadAndDownLoadPackageWithDotCsNames()
        {
            string packageId = DateTime.Now.Ticks.ToString() +  testContextInstance.TestName +".Cs";
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId);
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }

        [TestMethod]
        [Description("Creates a test bomb package and pushes it using Nuget.exe. Downloads the package again to make sure it works fine.")]
        [Priority(2)]
        [Ignore] //This method is marked ignore as we don't it to be run in regular runs. It will be run only when required.
        public void UploadAndDownLoadTestBombPackage()
        {
            string packageId = DateTime.Now.Ticks.ToString() + testContextInstance.TestName;
            string version = "1.0.0";
            string packageFullPath = PackageCreationHelper.CreateGalleryTestBombPackage(packageId);
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.V2FeedRootUrl);

            //Delete package from local disk so once it gets uploaded
            if (File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }

            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }

    }
}
