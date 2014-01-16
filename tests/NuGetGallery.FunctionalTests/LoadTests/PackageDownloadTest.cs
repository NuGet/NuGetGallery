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
        [Description("Tries to download a packages from v2 feed and make sure the re-direction happens properly.")]
        [Priority(0)]
        public void TryDownloadPackage()
        {           
            string packageId = "EntityFramework"; //try to down load a pre-defined test package.   
            string version = "5.0.0";
            //Just try download and not actual download. Since this will be used in load test, we don't to actually download the nupkg everytime.
            string redirectUrl = ODataHelper.TryDownloadPackageFromFeed(packageId, version).Result;
            Assert.IsNotNull( redirectUrl, " Package download from V2 feed didnt work");    
            string expectedSubString = "packages/entityframework.5.0.0.nupkg";
            Assert.IsTrue(redirectUrl.Contains(expectedSubString), " The re-direct Url {0} doesnt contain the expect substring {1}",redirectUrl ,expectedSubString); 
        }
    }
}
