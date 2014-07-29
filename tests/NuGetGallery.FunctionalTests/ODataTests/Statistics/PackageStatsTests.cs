using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.IO;
using System.Net;

namespace NuGetGallery.FunctionalTests.ODataTests.Statistics
{
    [TestClass]
    public class PackageStatsTests
    {
        /// <summary>
        /// Double-checks whether expected fields exist in the packages feed.
        /// </summary>
        [TestMethod]
        [Description("Verify the webresponse for stats/downloads/last6weeks/ returns all 6 fields")]
        [Priority(1)]
        public void PackageFeedStatsSanityTest()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            string firstPackage = responseText.Substring(responseText.IndexOf("{"), responseText.IndexOf("}") - responseText.IndexOf("{"));

            Assert.IsTrue(firstPackage.Contains(@"""PackageId"": """), "Expected PackageId field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""PackageVersion"": """), "Expected PackageVersion field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""Gallery"": """), "Expected Gallery field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""PackageTitle"": """), "Expected PackageTitle field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""PackageIconUrl"": """), "Expected PackageIconUrl field is missing.");
            Assert.IsTrue(firstPackage.Contains(@"""Downloads"": "), "Expected PackageIconUrl field is missing.");
        }

        /// <summary>
        /// Verify copunt querystring parameter in the Packages feed.
        /// </summary>
        [TestMethod]
        [Description("Verify the webresponse for stats/downloads/last6weeks/ contains the right amount of packages")]
        [Priority(2)]
        public void PackageFeedCountParameterTest()
        {
            WebRequest request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            // Get the response.          
            WebResponse response = request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();
            string[] separators = new string[1] { "}," };
            int packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            // Only verify the stats feed contains 500 packages for production
            if (UrlHelper.BaseUrl.ToLowerInvariant() == Constants.NuGetOrgUrl)
            {
                Assert.IsTrue(packageCount == 500, "Expected feed to contain 500 packages. Actual count: " + packageCount);
            }

            request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks?count=5");
            // Get the response.          
            response = request.GetResponse();
            sr = new StreamReader(response.GetResponseStream());
            responseText = sr.ReadToEnd();

            packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(packageCount == 5, "Expected feed to contain 5 packages. Actual count: " + packageCount);
        }

        /// <summary>
        /// Send a bogus request to Metrices Service endpoint and make sure the service is not broken by it.
        /// </summary>
        [TestMethod]
        [Description("Verify the result is Accepted after sending a bogus request to Metrices Service endpoint")]
        [Priority(1)]
       public void SendBogusToMetricsEndPoint()
       {
           string basics = "\"title\": \"Sample Konfabulator Widget\"," + "\"name\": \"main_window\"," + "\"width\": 500," + "\"height\": 500,";
           string jstring = "{" + basics + basics + basics + basics + "\"id\": \"dotnetrdf\"," + "\"version\": \"1.0.3\"" + "}";
           JObject bogus = JObject.Parse(jstring);
           bool Value = MetricsServiceHelper.TryHitMetricsEndPoint(bogus).Result;
           Assert.IsTrue(Value);
       }
    }
}
