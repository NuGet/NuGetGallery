using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests
{
    [TestClass]
    public class NuGetCoreTests : GalleryTestBase
    {       
        [TestMethod]
        [Description("Downloads a package from the server and validates that the file is present in the local disk")]
        [Priority(0)]
        public void DownloadPackageWithNuGetCoreTest()
        {
            string packageId = Constants.TestPackageId; //try to down load a pre-defined test package - BaseTestPackage.            
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }
    }
}
