using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGet;
using NuGetGallery.FunctionTests.Helpers;
using NuGetGallery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI;
using NuGetGallery.Infrastructure;
using Elmah;

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
            if (!EnvironmentSettings.RunFunctionalTests.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Functional tests are disabled in the current run. Please set environment variable RunFuntionalTests to True to enable them");
            }
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
            TableErrorLog log = new TableErrorLog(@"DefaultEndpointsProtocol=https;AccountName=" + Environment.GetEnvironmentVariable("StorageAccount") + @";AccountKey=""" + Environment.GetEnvironmentVariable("StorageAccessKey") + @"""");
            List<ErrorLogEntry> entities = new List<ErrorLogEntry>();
            log.GetErrors(0, 1000, entities);
            //this gets the error logs in the last ten minutes  
            entities = entities.FindAll(entity => DateTime.Now.Subtract(entity.Error.Time) > new TimeSpan(0, 10, 0));
            if (entities != null)
            {
                foreach (ErrorLogEntry entity in entities)
                {
                    Assert.Inconclusive(String.Format("ELMAH log error found:  {0}, {1}, {2}", entity.Error.Message, entity.Error.Time.ToString(), entity.Error.StatusCode));  
                }
            }  

        }
     


    }
}
