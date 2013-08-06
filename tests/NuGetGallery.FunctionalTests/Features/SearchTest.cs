using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]    
    public class SearchTest
    {
        private TestContext testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        [Description("Performs a querystring-based search of the v1 feed.  Confirms expected packages are returned.")]
        public void SearchV1Feed()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V1FeedRootUrl + @"Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='asp.net%20web%20helpers'&targetFramework='net40'&includePrerelease=false");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Assert.IsTrue(responseText.Contains(@"<title type=""text"">ASP.NET Web Helpers Library</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsTrue(responseText.Contains(@"<content type=""application/zip"" src=""" + UrlHelper.V1FeedRootUrl + "package/microsoft-web-helpers/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsFalse(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);

        }

        [TestMethod]
        [Description("Performs a querystring-based search of the default (non-curated) v2 feed.  Confirms expected packages are returned.")]
        public void SearchV2Feed()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='asp.net%20web%20helpers'&targetFramework='net40'&includePrerelease=false");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Assert.IsTrue(responseText.Contains(@"<title type=""text"">microsoft-web-helpers</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsTrue(responseText.Contains(@"<content type=""application/zip"" src=""" + UrlHelper.V2FeedRootUrl + "package/microsoft-web-helpers/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsFalse(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);

        }

    }
}
