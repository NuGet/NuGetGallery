using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NugetClientSDKHelpers;
using System.IO;

namespace NuGetGalleryBVTs.ClientIntegrationTests
{
    /// <summary>
    /// Tries to download and upload package from the gallery using NuGet.exe client.
    /// </summary>
    [TestClass]
    public class NugetCommandLineTests
    {
        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            //update Nuget.exe in class init so that the latest one is being used always.
            //Also clear the machine cache to make sure that we always hit the gallery
            CmdLineHelper.UpdateNugetExe();
            ClientSDKHelper.ClearMachineCache();
        }

        [TestMethod]
        [Description("Downloads a package using NuGet.exe and checks if the package file is present in the output dir")]
        public void DownPackageWithNuGetCommandLineTest()
        {
           string packageId = "Ninject";
           ClientSDKHelper.ClearLocalPackageFolder(packageId);
           int exitCode = CmdLineHelper.InstallPackage(packageId, Utilities.FeedUrl);
           Assert.IsTrue((exitCode == 0), "The package install via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
           Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");

        }

        [TestMethod]
        [Description("Creates a test package and pushes it to the server using Nuget.exe")]
        public void UploadPackageWithNuGetCommandLineTest()
        {
           // The API key is part of the nuget.config file that is present under the solution dir.
           string packageId = DateTime.Now.Ticks.ToString();
           string packageFullPath = NugetClientSDKHelpers.CmdLineHelper.CreatePackage(packageId);
           int exitCode = NugetClientSDKHelpers.CmdLineHelper.UploadPackage(packageFullPath, Utilities.FeedUrl);
           Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
           Assert.IsTrue(ClientSDKHelper.CheckIfPackageExistsInGallery(packageId), "Package {0} is not found in the site {1} after uploading.", packageId, Utilities.FeedUrl);

        }

    }
}
