using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NugetClientSDKHelpers;
using NuGet;
using System.IO;

namespace NuGetGalleryBVTs.TestBase
{
    [TestClass]
    public class GalleryTestBase
    {
        #region InitializeMethods
        [AssemblyInitialize()]
        public static void ClassInit(TestContext context)
        {
            //update Nuget.exe in class init so that the latest one is being used always.            
            CmdLineHelper.UpdateNugetExe();           
        }    

        [TestInitialize()]
        public void TestInit()
        {           
            //Clear the machine cache during the start of every test to make sure that we always hit the gallery         .
            ClientSDKHelper.ClearMachineCache();
        }
        #endregion InitializeMethods

        #region BaseMethods

        public void UploadNewPackageAndVerify(string packageId,string version="1.0.0")
        {            
            if (string.IsNullOrEmpty(packageId))
            {
                packageId = DateTime.Now.Ticks.ToString();
            }
            string packageFullPath = NugetClientSDKHelpers.CmdLineHelper.CreatePackage(packageId,version);
            int exitCode = NugetClientSDKHelpers.CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.V2FeedRootUrl);
            
            //Delete package from local disk so once it gets uploaded
            if (File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }
        }

        public void DownloadPackageAndVerify(string packageId)
        {
            ClientSDKHelper.ClearMachineCache();
            ClientSDKHelper.ClearLocalPackageFolder(packageId);
            new PackageManager(PackageRepositoryFactory.Default.CreateRepository(UrlHelper.V2FeedRootUrl), Environment.CurrentDirectory).InstallPackage(packageId);
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");
        }
        #endregion BaseMethods
    }
}
