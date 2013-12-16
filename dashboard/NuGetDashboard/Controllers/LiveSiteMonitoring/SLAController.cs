using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using NuGetDashboard.Utilities;
using DotNet.Highcharts.Helpers;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    /// <summary>
    /// Provides details about the server side SLA : Error rate, throughout, Response time.
    /// </summary>
    public class SLAController : Controller
    {
        public ActionResult Index()
        {            
            return PartialView("~/Views/SLA/SLA_Index.cshtml" );
        }

        [HttpGet]
        public ActionResult Now()
        {
           return PartialView("~/Views/SLA/SLA_Now.cshtml");
        }
      
        [HttpGet]
        public ActionResult Details()
        {
            return PartialView("~/Views/SLA/SLA_Details.cshtml");
        }

        [HttpGet]
        public ActionResult ErrorRate()
        {           
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("ErrorRate.json", "FailedRequests", "ErrorRate");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public ActionResult Throughput()
        {         
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("Throughput.json", "Throughput", "ThroughputPerMinute");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public JsonResult GetResponseTime()
        {
            string responseTime = BlobStorageService.GetValueFromBlob("SLASummary.json", "ResponseTime");
            return Json(responseTime, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetResponseTime()
        {
            string responseTime = BlobStorageService.GetValueFromBlob("SLASummary.json", "Throughput");
            return Json(responseTime, JsonRequestBehavior.AllowGet);
        }

    }
}
