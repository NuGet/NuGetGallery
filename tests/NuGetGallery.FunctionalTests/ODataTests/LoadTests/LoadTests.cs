using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

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
        public void DownloadPackageSimulationTest()
        {           
            string packageId = "EntityFramework"; //try to down load a pre-defined package.   
            string version = "5.0.0";
            //Just try download and not actual download. Since this will be used in load test, we don't to actually download the nupkg everytime.
            string redirectUrl = ODataHelper.TryDownloadPackageFromFeed(packageId, version).Result;
            Assert.IsNotNull( redirectUrl, " Package download from V2 feed didnt work");    
            string expectedSubString = "packages/entityframework.5.0.0.nupkg";
            Assert.IsTrue(redirectUrl.Contains(expectedSubString), " The re-direct Url {0} doesnt contain the expect substring {1}",redirectUrl ,expectedSubString); 
        }


        [TestMethod]
        [Description("Tries to simulate the launch of Manage package dialog UI")]
        [Priority(0)]
        public void ManagePackageUILaunchSimulationTest()
        {
            // api/v2/search()/$count query is made everytime Manage package UI is launched in VS.
            //This test simulates the same.
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/Search()/$count?$filter=IsLatestVersion&searchTerm=''&targetFramework='net45'&includePrerelease=false");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            int searchCount = Convert.ToInt32(responseText);
            //Just check if the response is a valid int.
            Assert.IsTrue(searchCount >= 0);
        }

        [TestMethod]
        [Description("Verify the webresponse for FindPackagesById with predefined packageId")]
        [Priority(0)]
        public void FindPackagesByIdForPredefinedPackage()
        {
            string packageId = "PostSharp";
            string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId;
            Assert.IsTrue(ODataHelper.ContainsResponseText(url, expectedText));
        }

        [TestMethod]
        [Description("Verify the webresponse for FindPackagesById with specific packageId and version")]
        [Priority(0)]
        public void FindPackagesBySpecificIdAndVersion()
        {
            string packageId = "Microsoft.Web.Infrastructure";
            string version = "1.0.0.0";
            string url = UrlHelper.V2FeedRootUrl + @"Packages(Id='" + packageId + "',Version='" + version + "')";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='" + version + "')</id>";
            Assert.IsTrue(ODataHelper.ContainsResponseText(url, expectedText));
        }

        [TestMethod]
        [Description("Verify the webresponse for PackagesApi test with specific packageId ")]
        [Priority(0)]
        public void PackagesApiTest()
        {
            string packageId = "newtonsoft.json";
            string url = UrlHelper.V2FeedRootUrl + @"Packages()?$filter=tolower(Id) eq '" + packageId + "'&$orderby=Id";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId;
            Assert.IsTrue(ODataHelper.ContainsResponseTextIgnoreCase(url, expectedText));
        }

        [TestMethod]
        [Description("Verify the webresponse for PackagesApi test with specific packageId ")]
        [Priority(1)]
        public void StatsTotalTest()
        {           
            string url = UrlHelper.BaseUrl + @"/stats/totals";
            Assert.IsTrue(ODataHelper.ContainsResponseText(url, @"Downloads"));
        }

        [TestMethod]
        [Description("Hits the search endpoint directly")]
        [Priority(0)]
        public void HitSearchEndPointDirectly()
        {
            bool Value = TrySearch().Result;
            Assert.IsTrue(Value);
        }

       public static async Task<bool> TrySearch()
       {
           try
           {
                HttpClientHandler handler = new HttpClientHandler();
                handler.AllowAutoRedirect = false;
                using (HttpClient client = new HttpClient(handler))
                {
                    string requestUri = "https://api-search-0.nuget.org/search/query?q='app insights'&luceneQuery=false";
                    var response = await client.GetAsync(requestUri);
                    //print the header 
                    Console.WriteLine("HTTP status code : {0}", response.StatusCode);
                    //Console.WriteLine("HTTP header : {0}", response.Headers.ToString());
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (HttpRequestException hre)
            {
                Console.WriteLine("Exception : {0}", hre.Message);
                return false;
            }
        }

       [TestMethod]
       [Description("Hits the metrics service endpoint directly")]
       [Priority(0)]
       public void HitMetricsEndPointDirectly()
       {
           bool Value = MetricsServiceHelper.TryHitMetricsEndPoint("RIAServices.Server", "4.2.0", "120.0.0.0", "NuGet Load Tests/Metrics Service", "Test", "None", null);
           Assert.IsTrue(Value);
       }    
    }
}
