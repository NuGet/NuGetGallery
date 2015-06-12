using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests.LoadTests
{
    /// <summary>
    /// This class has the various scenarios used in LoadTests.
    /// The tests does minimal validation and uses existing packages to reduce the execution time spent in test prep and asserts.
    /// </summary>
    [TestClass]
    public class LoadTests
    {
        [TestMethod]
        [Description("Tries to download a packages from v2 feed and make sure the re-direction happens properly.")]
        [Priority(0)]
        public async Task DownloadPackageSimulationTest()
        {
            string packageId = "EntityFramework"; //try to down load a pre-defined package.
            string version = "5.0.0";
            //Just try download and not actual download. Since this will be used in load test, we don't to actually download the nupkg everytime.
            string redirectUrl = await ODataHelper.TryDownloadPackageFromFeed(packageId, version);
            Assert.IsNotNull(redirectUrl, " Package download from V2 feed didnt work");
            string expectedSubString = "packages/entityframework.5.0.0.nupkg";
            Assert.IsTrue(redirectUrl.Contains(expectedSubString), " The re-direct Url {0} doesnt contain the expect substring {1}", redirectUrl, expectedSubString);
        }


        [TestMethod]
        [Description("Tries to simulate the launch of Manage package dialog UI")]
        [Priority(0)]
        public async Task ManagePackageUILaunchSimulationTest()
        {
            // api/v2/search()/$count query is made everytime Manage package UI is launched in VS.
            //This test simulates the same.
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/Search()/$count?$filter=IsLatestVersion&searchTerm=''&targetFramework='net45'&includePrerelease=false");
            var response = await request.GetResponseAsync();
            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }
            int searchCount = Convert.ToInt32(responseText);

            //Just check if the response is a valid int.
            Assert.IsTrue(searchCount >= 0);
        }

        [TestMethod]
        [Description("Verify the webresponse for FindPackagesById with predefined packageId")]
        [Priority(0)]
        public async Task FindPackagesByIdForPredefinedPackage()
        {
            string packageId = "PostSharp";
            string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId;
            var containsResponseText = await ODataHelper.ContainsResponseText(url, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse for FindPackagesById with specific packageId and version")]
        [Priority(0)]
        public async Task FindPackagesBySpecificIdAndVersion()
        {
            string packageId = "Microsoft.Web.Infrastructure";
            string version = "1.0.0.0";
            string url = UrlHelper.V2FeedRootUrl + @"Packages(Id='" + packageId + "',Version='" + version + "')";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='" + version + "')</id>";
            var containsResponseText = await ODataHelper.ContainsResponseText(url, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse for PackagesApi test with specific packageId ")]
        [Priority(0)]
        public async Task PackagesApiTest()
        {
            string packageId = "newtonsoft.json";
            string url = UrlHelper.V2FeedRootUrl + @"Packages()?$filter=tolower(Id) eq '" + packageId + "'&$orderby=Id";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId;
            var containsResponseText = await ODataHelper.ContainsResponseTextIgnoreCase(url, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse for PackagesApi test with specific packageId ")]
        [Priority(1)]
        public async Task StatsTotalTest()
        {
            string url = UrlHelper.BaseUrl + @"/stats/totals";
            var containsResponseText = await ODataHelper.ContainsResponseText(url, @"Downloads");
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Hits the search endpoint directly")]
        [Priority(0)]
        public async Task HitSearchEndPointDirectly()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;

            string requestUri = "http://nuget-prod-0-v2searchwebsite.azurewebsites.net/search/query?q='app insights'&luceneQuery=false";
            HttpResponseMessage response;

            using (var client = new HttpClient(handler))
            {
                response = await client.GetAsync(requestUri);
            }

            Console.WriteLine("HTTP status code : {0}", response.StatusCode);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [Description("Hits the metrics service endpoint directly")]
        [Priority(0)]
        public async Task HitMetricsEndPointDirectly()
        {
            bool value = await MetricsServiceHelper.TryHitMetricsEndPoint("RIAServices.Server", "4.2.0", "120.0.0.0", "NuGet Load Tests/Metrics Service", "Test", "None", null);
            Assert.IsTrue(value);
        }
    }
}
