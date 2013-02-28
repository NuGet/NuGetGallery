using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.TestBase;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class PackageVersion : GalleryTestBase
    {
        [TestMethod]
        [Description("Upload multiple versions of a package and see if it gets uploaded properly")]
        public void UploadMultipleVersionOfPackage()
        {
            string packageId = "TestMultipleVersion" + "." + DateTime.Now.ToString();
            base.UploadNewPackageAndVerify(packageId, "1.0.0");
            base.UploadNewPackageAndVerify(packageId, "2.0.0");
            int actualCount = ClientSDKHelper.GetVersionCount(packageId);
            Assert.IsTrue(actualCount.Equals(2), " 2 versions of package {0} not found after uploading. Actual versions found {1}", packageId, actualCount);
        }
    }
}
