using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NugetClientSDKHelpers;
using NuGet;
using NuGetGalleryBVTs.TestBase;

namespace NuGetGalleryBVTs
{
    [TestClass]
    public class NuGetCoreTests : GalleryTestBase
    {       
        [Description("Downloads a package from the server and validates that the file is present in the local disk"), TestMethod]
        public void DownloadPackageWithNuGetCoreTest()
        {
            string packageId = "Ninject";
            base.DownloadPackageAndVerify(packageId);
        }


    }
}
