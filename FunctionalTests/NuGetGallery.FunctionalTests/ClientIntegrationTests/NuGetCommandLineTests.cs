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


    }
}
