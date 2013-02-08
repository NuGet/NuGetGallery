using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NugetClientSDKHelpers;
using NuGet;

namespace NuGetGalleryBVTs
{
    [TestClass]
    public class NuGetCoreTests
    {       
        [Description("Downloads a package from the server and validates that the file is present in the local disk"), TestMethod]
        public void DownloadPackageWithNuGetCoreTest()
        {
            string packageId = "jQuery";
            ClientSDKHelper.ClearMachineCache();
            ClientSDKHelper.ClearLocalPackageFolder(packageId);
            new PackageManager(PackageRepositoryFactory.Default.CreateRepository(Utilities.FeedUrl), Environment.CurrentDirectory).InstallPackage(packageId);
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");
        }


    }
}
