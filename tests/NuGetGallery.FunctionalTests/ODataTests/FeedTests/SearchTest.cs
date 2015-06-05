using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.Features
{
    [TestClass]
    public class SearchTest
    {
        [TestMethod]
        [Description("Performs a querystring-based search of the v1 feed.  Confirms expected packages are returned.")]
        [Priority(0)]
        public async Task SearchV1Feed()
        {
            await SearchFeedAsync(UrlHelper.V1FeedRootUrl, "ASP.NET Web Helpers Library");
        }

        [TestMethod]
        [Description("Performs a querystring-based search of the default (non-curated) v2 feed.  Confirms expected packages are returned.")]
        [Priority(0)]
        public async Task SearchV2Feed()
        {
            await SearchFeedAsync(UrlHelper.V2FeedRootUrl, "microsoft-web-helpers");
        }

        private static async Task SearchFeedAsync(string feedRootUrl, string title)
        {
            var request = WebRequest.Create(feedRootUrl + @"Search()?$filter=IsLatestVersion&$skip=0&$top=10&searchTerm='asp.net%20web%20helpers'&targetFramework='net40'&includePrerelease=false");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            Assert.IsTrue(responseText.Contains(@"<title type=""text"">" + title + @"</title>"), "The expected package title wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsTrue(responseText.Contains(@"<content type=""application/zip"" src=""" + feedRootUrl + "package/microsoft-web-helpers/"), "The expected package URL wasn't found in the feed.  Feed contents: " + responseText);
            Assert.IsFalse(responseText.Contains(@"jquery"), "The feed contains non-matching package names.  Feed contents: " + responseText);
        }
    }
}
