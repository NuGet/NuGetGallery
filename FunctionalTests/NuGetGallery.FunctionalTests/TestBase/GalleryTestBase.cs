using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGet;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI;

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
        public static void ClassInit(TestContext context)
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

        

     


    }
}
