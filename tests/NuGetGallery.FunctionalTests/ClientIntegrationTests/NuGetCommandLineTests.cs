using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.IO;
using System.Threading;

namespace NuGetGallery.FunctionalTests.ClientIntegrationTests
{
    /// <summary>
    /// Tries to download and upload package from the gallery using NuGet.exe client.
    /// </summary>
    [TestClass]
    public class NugetCommandLineTests : GalleryTestBase
    {
        [TestMethod]
        [Description("Downloads a package using NuGet.exe and checks if the package file is present in the output dir")]
        [Priority(0)]
        public void DownloadPackageWithNuGetCommandLineTest()
        {
           string packageId = Constants.TestPackageId; //try to down load a pre-defined test package.          
           ClientSDKHelper.ClearLocalPackageFolder(packageId);
           int exitCode = CmdLineHelper.InstallPackage(packageId, UrlHelper.V2FeedRootUrl, Environment.CurrentDirectory);
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
        [Description("Creates a test package with minclientversion tag and .cs name. Pushes it to the server using Nuget.exe and then download via ClientSDK")]
        [Priority(0)]
        public void UploadAndDownLoadPackageWithMinClientVersion()
        {
            string packageId = DateTime.Now.Ticks.ToString() + "PackageWithDotCsNames.Cs";
            string version = "1.0.0";
            string packageFullPath = PackageCreationHelper.CreatePackageWithMinClientVersion(packageId, version, "2.3");        
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt succeed properly. Check the logs to see the process error and output stream");
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.V2FeedRootUrl);

            // Delete package from local disk so once it gets uploaded
            if (File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }
    }
}
