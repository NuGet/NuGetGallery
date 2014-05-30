using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Net;

namespace NuGetGallery.FunctionalTests.TestBase
{
    /// <summary>
    /// Base class for all the test classes. Has the common functions which individual test classes would use.
    /// </summary>
    [TestClass]   
    public class GalleryTestBase
    {
        #region InitializeMethods

        [AssemblyInitialize()]
        public static void AssemblyInit(TestContext context)
        {
            //Check if functional tests is enabled. If not, do an assert inconclusive.
#if DEBUG
#else
            if (!EnvironmentSettings.RunFunctionalTests.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Functional tests are disabled in the current run. Please set environment variable RunFuntionalTests to True to enable them");
            }
#endif
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; }; //supress SSL validation so that we can run tests against staging slot as well.
            CheckIfBaseTestPackageExists();
        }

        public static void CheckIfBaseTestPackageExists()
        {
            //Check if the BaseTestPackage exists in current source and if not upload it. This will be used by the download related tests.
            try
            {
                if (!ClientSDKHelper.CheckIfPackageExistsInSource(Constants.TestPackageId, UrlHelper.V2FeedRootUrl))
                {
                    AssertAndValidationHelper.UploadNewPackageAndVerify(Constants.TestPackageId);
                }
            }
            catch (AssertFailedException)
            {
                Assert.Inconclusive("The initialization method to pre-upload test package has failed. Hence failing all the tests. Make sure that a package by name {0} exists @ {1} before running tests. Check test run error for details", Constants.TestPackageId, UrlHelper.BaseUrl);
            }
        }

        [TestInitialize()]
        public void TestInit()
        {           
            //Clear the machine cache during the start of every test to make sure that we always hit the gallery         .
            ClientSDKHelper.ClearMachineCache();
        }
        #endregion InitializeMethods

        
        [AssemblyCleanup()]
        public static void CleanAssembly()
        {
         
        }
    }
}
