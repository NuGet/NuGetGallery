namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NuGetGallery.FunctionalTests.TestBase;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class AggregateStatsInHomePage : WebTest
    {
        /// <summary>
        /// Verifies that the aggregate stats in home page gets incremented appropriately after downloading a package.
        /// </summary>
        public AggregateStatsInHomePage()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to the stats/totals to get the initial count.
            WebTestRequest statsRequestBeforeDownload = GetWebRequestForAggregateStats();
            yield return statsRequestBeforeDownload;
            statsRequestBeforeDownload = null;

            int aggregateStatsBeforeDownload = GetAggregateStatsFromContext();

            //upload a new package and download it.
            string packageId = DateTime.Now.Ticks.ToString() + this.Name;
            AssertAndValidationHelper.UploadNewPackageAndVerify(packageId);
            AssertAndValidationHelper.DownloadPackageAndVerify(packageId);

            //wait either the download count changes or till 5 minutes which ever is earlier.
            int waittime = 0;
            int aggregateStatsAfterDownload = aggregateStatsBeforeDownload;
            while (aggregateStatsAfterDownload == aggregateStatsBeforeDownload && waittime <= 300)
            {
                //send a request stats to keep polling the new download count.
                statsRequestBeforeDownload = GetWebRequestForAggregateStats();
                yield return statsRequestBeforeDownload;
                statsRequestBeforeDownload = null;

                aggregateStatsAfterDownload = GetAggregateStatsFromContext();
                System.Threading.Thread.Sleep(30 * 1000);//sleep for 30 seconds.
                waittime += 30;
            }

            //check download count. New download count should be old one + 1.
            Assert.IsTrue( aggregateStatsBeforeDownload == (aggregateStatsAfterDownload -1 ), "Aggregate stats count is not increased by 1 after downloading. Aggregate stats before download :{0}. Stats after download : {1}", aggregateStatsBeforeDownload, aggregateStatsAfterDownload);
        }

      

        #region PrivateMethods

        private ExtractText GetExtractionRuleForDownloadCount()
        {
            //Extract the download count value from the response.
            ExtractText extractDownLoadCount = new ExtractText();
            extractDownLoadCount.StartsWith = "\"Downloads\":\"";
            extractDownLoadCount.EndsWith = "\",\"";
            extractDownLoadCount.IgnoreCase = true;
            extractDownLoadCount.UseRegularExpression = false;
            extractDownLoadCount.Required = true;
            extractDownLoadCount.ExtractRandomMatch = false;
            extractDownLoadCount.Index = 0;
            extractDownLoadCount.HtmlDecode = true;
            extractDownLoadCount.ContextParameterName = "DownloadCount";
            return extractDownLoadCount;
        }

        private WebTestRequest GetWebRequestForAggregateStats()
        {
            //send a request to the stats/totals.
            WebTestRequest statsRequestBeforeDownload = new WebTestRequest(UrlHelper.AggregateStatsPageUrl);
            //Extract the download count value from the response.
            ExtractText extractDownLoadCount = GetExtractionRuleForDownloadCount();
            statsRequestBeforeDownload.ExtractValues += new EventHandler<ExtractionEventArgs>(extractDownLoadCount.Extract);
            return statsRequestBeforeDownload;

        }

        private int GetAggregateStatsFromContext()
        {
            int aggregateStatsBeforeDownload = Convert.ToInt32(this.Context["DownloadCount"].ToString().Replace(",", "").Replace(".", ""));
            return aggregateStatsBeforeDownload;
        }
        #endregion PrivateMethods
    }
}
