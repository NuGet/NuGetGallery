using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Trending
{
    /// <summary>
    /// Shows data from google analytics like PageLoadTime for various pages, response time based on geography.
    /// </summary>
    public class AnalyticsController : Controller
    {
     
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Details()
        {         
            List<string> xValues = new List<string>();
            List<Object> yValues = new List<Object>();
            BlobStorageService.GetJsonDataFromBlob("PageLoadTime.json", out xValues, out yValues);         
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetBarChart(xValues, yValues, "PageLoadTime", "PageLoadTime");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        public ActionResult PerCountry()
        {
            //List<PingdomPerCountryViewModel> checks = new List<PingdomPerCountryViewModel>();
            List<string> xValues = new List<string>();
            List<Object> yValues = new List<Object>();
            BlobStorageService.GetJsonDataFromBlob("PerCountry.json", out xValues, out yValues);
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetBarChart(xValues, yValues, "UpTime", "ResponseTimePerCountry");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

    }
}
