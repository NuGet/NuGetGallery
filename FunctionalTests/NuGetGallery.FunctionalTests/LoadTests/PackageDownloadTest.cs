using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NuGetGallery.FunctionalTests.LoadTests
{
    [TestClass]
    public class PackageDownloadTest
    {
        [TestMethod]
        [Description("Downloads a package from the V2 feed and checks if the file is present on local disk")]
        [Priority(0)]
        public void CheckIfAPIV2PackageFeedIsUp()
        {           
            string packageId = "EntityFramework"; //try to down load a pre-defined test package.   
            string version = "5.0.0";
            string redirectUrl = ODataHelper.TryDownloadPackageFromFeed(packageId, version);
            Assert.IsNotNull( redirectUrl, " Package download from V2 feed didnt work");    
            string expectedSubString = "packages/entityframework.5.0.0.nupkg";
            Assert.IsTrue(redirectUrl.Contains(expectedSubString), " The re-direct Url {0} doesnt contain the expect substring {1}",redirectUrl ,expectedSubString); 
        }
    }
}
