using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using NuGetGallery.FunctionalTests.TestBase;
using System.IO;

namespace NuGetGallery.FunctionalTests.ODataTests 
{
    [TestClass]
    public partial class V2FeedTest : GalleryTestBase
    {
        [TestMethod]
        public void GetUpdatesTest()
        {
        }

        [TestMethod]
        public void FindPackagesByIdTest()
        {
            string packageId = "TestV2FeedFindPackagesById" + "." + DateTime.Now.Ticks.ToString();
            base.UploadNewPackageAndVerify(packageId, "1.0.0");
            base.UploadNewPackageAndVerify(packageId, "2.0.0");
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId +"'");          
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            Assert.IsTrue(responseText.Contains(@" <id>"+ UrlHelper.V2FeedRootUrl + "Packages(Id='"+ packageId + "',Version='1.0.0')</id>"));
            Assert.IsTrue(responseText.Contains(@" <id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='2.0.0')</id>"));
           
        }
    }
}
