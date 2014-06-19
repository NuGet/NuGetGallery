using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.IO;

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
           Assert.IsTrue((exitCode == 0), Constants.PackageDownloadFailureMessage);
           Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), Constants.PackageInstallFailureMessage);
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
            Assert.IsTrue((exitCode == 0), Constants.UploadFailureMessage);
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), Constants.PackageNotFoundAfterUpload, packageId, UrlHelper.V2FeedRootUrl);

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
