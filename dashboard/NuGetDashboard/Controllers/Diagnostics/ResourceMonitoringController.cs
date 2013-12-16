using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Provides details about the resource utilization on the server : CPU, memory, Disk, DB.
    /// </summary>
    public class ResourceMonitoringController : Controller
    {
        public ActionResult Index()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Index.cshtml");
        }

        [HttpGet]
        public ActionResult Details()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Details.cshtml");
        }

        [HttpGet]
        public JsonResult DownloadLog()
        {
            return Json(BlobStorageService.DownloadLatest("wad-iis-requestlogs") , JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult DBQueries()
        {
            //Returns the chart for Average response for the last week       
            Dictionary<string, string> queries = new Dictionary<string, string>();
            queries = BlobStorageService.GetDictFromBlob("DBQueries.json");
            return PartialView("~/Views/Analytics/Analytics_Details.cshtml", queries);

        }   

        [HttpGet]
        public ActionResult DBConnections()
        {  
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("DBConnections.json", "DBConnections", "DBConnections");
           return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public ActionResult Instance0CPU()
        {
           DotNet.Highcharts.Highcharts chart =  ChartingUtilities.GetSingleLineChartForBlob("Instance0CPU.json","CPUTime","Instance0CPUPercentTime");
           return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

      
        [HttpGet]
        public ActionResult Instance1CPU()
        { 
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("Instance1CPU.json", "CPUTime", "Instance1CPUPercentTime");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }

        [HttpGet]
        public ActionResult Instance0Memory()
        {                
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("Instance0Memory.json", "Memory", "Instance0MemoryInBytes");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);

        }
        [HttpGet]
        public ActionResult Instance1Memory()
        {
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetSingleLineChartForBlob("Instance1Memory.json", "Memory", "Instance1MemoryInBytes");
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);
        }
    }
}
