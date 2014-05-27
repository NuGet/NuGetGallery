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
        [TestMethod]
        [Description("Performs a querystring-based search of the v1 feed.  Confirms expected packages are returned.")]
        public void SearchV1Feed()
        {
            SearchFeed(UrlHelper.V1FeedRootUrl);
        }

        [TestMethod]
        [Description("Performs a querystring-based search of the default (non-curated) v2 feed.  Confirms expected packages are returned.")]
        public void SearchV2Feed()
        {
            SearchFeed(UrlHelper.V2FeedRootUrl);
        }

        public void SearchFeed(string feedRootUrl)
        {
            WebRequest request = WebRequest.Create(feedRootUrl + @"Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='asp.net%20web%20helpers'&targetFramework='net40'&includePrerelease=false");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Assert.IsTrue(responseText.Contains(@"<title type=""text"">microsoft-web-helpers</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsTrue(responseText.Contains(@"<content type=""application/zip"" src=""" + feedRootUrl + "package/microsoft-web-helpers/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsFalse(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);
        }
    }
}
