using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System.IO;
using System.Net;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class StatisticsPageTest : NuGetFluentTest 
    {
        [TestMethod]
        [Description("Cross-check the contents of the Statistics page against the last6weeks API endpoint.")]
        [Priority(2)]
        public void StatisticsPage()
        {
            // Request the last 6 weeks endpoint.
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            // Grab the top 10 package ids in the feed.
            string[] packageName = new string[10];
            responseText = packageName[0] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """) + 14);
            packageName[0] = packageName[0].Substring(0, responseText.IndexOf(@""""));
            for (int i = 1; i < 10; i++)
            {
                responseText = packageName[i] = responseText.Substring(responseText.IndexOf(@"""PackageId"": """) + 14);
                packageName[i] = packageName[i].Substring(0, responseText.IndexOf(@""""));
            }

            // Navigate to the stats page.
            I.Open(UrlHelper.StatsPageUrl);
            I.Expect.Url(x => x.AbsoluteUri.Contains("stats"));

            for (int i = 0; i < 10; i++)
            {
                I.Expect.Exists("td:contains('" + packageName[i] + "')"); 
            }
            I.Expect.Exists("p:contains('Download statistics displayed on this page reflect the actual package downloads from the NuGet.org site.')");
        }
    }
}
