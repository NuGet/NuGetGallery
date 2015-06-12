using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NuGetGallery.FunctionTests.Helpers;

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
        public async Task PackageFeedStatsSanityTest()
        {
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

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
        public async Task PackageFeedCountParameterTest()
        {
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks/");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            string[] separators = { "}," };
            int packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            // Only verify the stats feed contains 500 packages for production
            if (UrlHelper.BaseUrl.ToLowerInvariant() == Constants.NuGetOrgUrl)
            {
                Assert.IsTrue(packageCount == 500, "Expected feed to contain 500 packages. Actual count: " + packageCount);
            }

            request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"stats/downloads/last6weeks?count=5");
            // Get the response.
            response = await request.GetResponseAsync();
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            packageCount = responseText.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(packageCount == 5, "Expected feed to contain 5 packages. Actual count: " + packageCount);
        }

        /// <summary>
        /// Send a bogus request to Metrices Service endpoint and make sure the service is not broken by it.
        /// </summary>
        [TestMethod]
        [Description("Verify the result is Accepted after sending a bogus request to Metrices Service endpoint")]
        [Priority(1)]
        public async Task SendBogusToMetricsEndPoint()
        {
            string basics = "\"title\": \"Sample Konfabulator Widget\"," + "\"name\": \"main_window\"," + "\"width\": 500," + "\"height\": 500,";
            string jstring = "{" + basics + basics + basics + basics + "\"id\": \"dotnetrdf\"," + "\"version\": \"1.0.3\"" + "}";
            JObject bogus = JObject.Parse(jstring);
            bool Value = await MetricsServiceHelper.TryHitMetricsEndPoint(bogus);
            Assert.IsTrue(Value);
        }
    }
}
