using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Models;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    /// <summary>
    /// Provides uptime details for the Gallery ( data retrieved from pingdom).
    /// </summary>
    public class UpTimeController : Controller
    {
        public ActionResult Index()
        {
            return PartialView("~/Views/UpTime/UpTime_Index.cshtml");
        }       
     
        [HttpGet]
        public ActionResult Now()
        {
            List<PingdomStatusViewModel> checks = GetStatusInternal();
            return PartialView("~/Views/UpTime/UpTime_Now.cshtml", checks);
        }

        public ActionResult Details()
        {
            return PartialView("~/Views/UpTime/UpTime_Details.cshtml");
        }
      
        [HttpGet]
        public ActionResult ThisWeek()
        {
            //Returns the chart for Average response for the last week
            string[] checkNames = new string[] { "feed", "home", "packages" };
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            List<string> xValues = new List<string>();
            List<Object> yValues = new List<Object>();
            foreach (string check in checkNames)
            {
                //Get the response values from pre-created blobs for each check.
                BlobStorageService.GetJsonDataFromBlob(check + "WeeklyReport.json", out xValues, out yValues);
                seriesSet.Add(new DotNet.Highcharts.Options.Series
                {
                    Data = new Data(yValues.ToArray()),
                    Name = check
                });
            }
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, xValues,"AvgResponseTime");         
            return PartialView("~/Views/UpTime/UpTime_ThisWeek.cshtml", chart);
        }       

        [HttpGet]
        public ActionResult Monthly()
        {
            string value = DateTimeUtility.GetLastMonthName();           
            string[] checkNames = new string[] { "feed", "home", "packages" };
            List<PingdomMonthlyReportViewModel> reports = new List<PingdomMonthlyReportViewModel>();
            foreach (string check in checkNames)
            {
                //Get the response values from pre-created blobs for each check.
                string blobName = check + value + "MonthlyReport.json";
                Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob(blobName);
                double upTime = Convert.ToDouble(dict["totalup"]);
                int avgResponse = Convert.ToInt32(dict["avgresponse"]);
                int Outages = Convert.ToInt32(dict["Outages"]);
                int downTime = Convert.ToInt32(dict["totaldown"]);
                reports.Add(new PingdomMonthlyReportViewModel(check, upTime, downTime, Outages, avgResponse, value));
            }          
            ViewBag.SelectedValue = value;
            return PartialView("~/Views/UpTime/UpTime_Monthly.cshtml",reports);
        }

        [HttpGet]
        public JsonResult GetStatus()
        {
            List<PingdomStatusViewModel> checks = GetStatusInternal();
            return Json(checks, JsonRequestBehavior.AllowGet);
        }
        

        #region PrivateMethods

        private List<PingdomStatusViewModel> GetStatusInternal()
        {

            List<PingdomStatusViewModel> checks = new List<PingdomStatusViewModel>();
            NetworkCredential nc = new NetworkCredential(ConfigurationManager.AppSettings["PingdomUserName"], ConfigurationManager.AppSettings["PingdomPassword"]);            
            WebRequest request = WebRequest.Create("https://api.pingdom.com/api/2.0/checks");
            request.Credentials = nc;
            request.Headers.Add(ConfigurationManager.AppSettings["PingdomAppKey"]);            
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var o in objects["checks"])
                {
                    if (o["name"].ToString().Contains("curated"))
                        continue;
                    checks.Add(new PingdomStatusViewModel(o["id"], o["name"], o["status"], o["lastresponsetime"]));
                }
            }
            return checks;
        }     

        #endregion
    }
}
