using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NugetClientSDKHelpers;
using System.IO;
using NuGetGalleryBVTs.TestBase;

namespace NuGetGalleryBVTs.ClientIntegrationTests
{
    /// <summary>
    /// Tries to download and upload package from the gallery using NuGet.exe client.
    /// </summary>
    [TestClass]
    public class NugetCommandLineTests : GalleryTestBase
    {
        [TestMethod]
        [Description("Downloads a package using NuGet.exe and checks if the package file is present in the output dir")]
        public void DownPackageWithNuGetCommandLineTest()
        {
           string packageId = "Ninject";
           ClientSDKHelper.ClearLocalPackageFolder(packageId);
           int exitCode = CmdLineHelper.InstallPackage(packageId, UrlHelper.V2FeedRootUrl);
           Assert.IsTrue((exitCode == 0), "The package install via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
           Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");

        }

        [TestMethod]
        [Description("Creates a test package and pushes it to the server using Nuget.exe")]
        public void UploadPackageWithNuGetCommandLineTest()
        {
            base.UploadNewPackageAndVerify(DateTime.Now.Ticks.ToString());
        }


    }
}
