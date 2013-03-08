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

        

     


    }
}
