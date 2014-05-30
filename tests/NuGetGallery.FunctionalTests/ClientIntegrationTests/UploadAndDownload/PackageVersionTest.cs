using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;
using System;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class PackageVersionTest : GalleryTestBase
    {
        [TestMethod]
        [Description("Upload multiple versions of a package and see if it gets uploaded properly")]
        [Priority(0)]
        public void UploadMultipleVersionOfPackage()
        {
            string packageId = "TestMultipleVersion" + "." + DateTime.Now.Ticks.ToString();
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "1.0.0");
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId, "2.0.0");
            int actualCount = ClientSDKHelper.GetVersionCount(packageId);
            Assert.IsTrue(actualCount.Equals(2), " 2 versions of package {0} not found after uploading. Actual versions found {1}", packageId, actualCount);
        }
    }
}
