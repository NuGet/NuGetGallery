using DotNet.Highcharts;
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
    /// This controller shows the trends of new package uploads, downloads and  users.
    /// </summary>
    public class TrendingController : Controller
    {        
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Details()
        {
            return PartialView("~/Views/Trending/Trending_Details.cshtml");
        }

        public ActionResult Monthly()
        {
            return PartialView("~/Views/Trending/Trending_Monthly.cshtml");
        }

        public ActionResult Daily()
        {
            return PartialView("~/Views/Trending/Trending_Details.cshtml");
        }

        //Returns the overall trend chart for packages 
        public ActionResult PackagesChart()
        {
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("UploadsoctoberMonthlyReport.json", "Packages", "Packages");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        //Returns the overall trend chart for Downloads
        public ActionResult DownloadsChart()
        {
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob ("DownloadsoctoberMonthlyReport.json","Downloads","Downloads");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        //Returns the overall trend chart for users
        public ActionResult UsersChart()
        {
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("UsersoctoberMonthlyReport.json", "Users", "Users");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }
    }
}
