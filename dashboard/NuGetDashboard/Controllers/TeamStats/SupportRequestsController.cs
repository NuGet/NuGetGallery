using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using NuGetDashboard.Models;
using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Options;
using DotNet.Highcharts.Helpers;

namespace NuGetDashboard.Controllers.TeamStats
{
    /// <summary>
    /// Provides details about the incoming support requests in Gallery.
    /// </summary>
    public class SupportRequestsController : Controller
    {
        public ActionResult Index()
        {   
            string requestCount = BlobStorageService.GetValueFromBlob("SupportRequestSummaryReport.json", "No. of open requests ");
            return PartialView("~/Views/SupportRequests/SupportRequests_Index.cshtml",requestCount);
        }

        public ActionResult Details()
        {
            //Get requests by category
            List<string> xValues = new List<string>();
            List<Object> yValues = new List<Object>();
            BlobStorageService.GetJsonDataFromBlob("SupportRequestsByCategoryReport.json", out xValues, out yValues);
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetBarChart(xValues, yValues,"RequestsPerCategory","SupportRequestsPerCategory");
            return PartialView("~/Views/SupportRequests/SupportRequests_Details.cshtml", chart);
        }
            

        public ActionResult Summary()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            return PartialView("~/Views/SupportRequests/SupportRequests_Summary.cshtml", BlobStorageService.GetDictFromBlob("SupportRequestSummaryReport.json"));            
        }


    }
}
