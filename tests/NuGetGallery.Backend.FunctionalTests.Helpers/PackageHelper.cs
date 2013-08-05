using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.FunctionalTests;

namespace NuGetOperations.FunctionalTests.Helpers
{
    /// <summary>
    /// This class just provides wrapper functions for helpers exposed by NuGetGallery.FunctionalTests/ Helpers
    /// </summary>
    public class PackageHelper
    {
        public static void UploadNewPackage(string packageId)
        {
            NuGetGallery.FunctionalTests.AssertAndValidationHelper.UploadNewPackageAndVerify(packageId);
        }

        public static void DownloadPackage(string packageId)
        {
            NuGetGallery.FunctionalTests.AssertAndValidationHelper.DownloadPackageAndVerify(packageId);
        }
    }
}
