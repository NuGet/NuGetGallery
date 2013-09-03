using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using NuGet;
using NuGetGallery.FunctionalTests.TestBase;

namespace NuGetGallery.FunctionalTests
{
    [TestClass]
    public class NuGetCoreTests : GalleryTestBase
    {       
        [Description("Downloads a package from the server and validates that the file is present in the local disk"), TestMethod]
        [Priority(0)]
        public void DownloadPackageWithNuGetCoreTest()
        {
            string packageId = Constants.TestPackageId; //try to down load a pre-defined test package.            
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }

    }
}
