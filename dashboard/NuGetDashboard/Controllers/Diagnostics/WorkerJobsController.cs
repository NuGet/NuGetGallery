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

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Provides details about the status of the worker jobs.
    /// </summary>
    public class WorkerJobsController : Controller
    {
       
        Dictionary<string, int> registeredJobs = new Dictionary<string, int>();

        public WorkerJobsController()
        {
            //TBD : The list of job names should be taken from the config.
            registeredJobs.Add("BackupDatabaseTask", 30);                      
            registeredJobs.Add("HandleQueuedPackageEdits", 30);
          
        }

        [HttpGet]
        public JsonResult GetWorkerStatus()
        {
            List<WorkerJobStatusViewModel> jobsModel = GetWorkerStatus_Internal();
            return Json(jobsModel, JsonRequestBehavior.AllowGet);
        }
               
        public ActionResult Index()
        {            
            return PartialView("~/Views/WorkerJobs/Worker_Index.cshtml" );
        }

        public ActionResult Details()
        {
            List<WorkerJobStatusViewModel> jobsModel = GetWorkerStatus_Internal();
            return PartialView("~/Views/WorkerJobs/Worker_Details.cshtml", jobsModel);
        }

        #region PrivateMethod

        private List<WorkerJobStatusViewModel> GetWorkerStatus_Internal()
        {
            List<WorkerJobStatusViewModel> jobsModel = new List<WorkerJobStatusViewModel>();
            foreach (KeyValuePair<string, int> job in registeredJobs)
            {
                WorkerJobStatusViewModel model = new WorkerJobStatusViewModel(job.Key);
                string blobName = job.Key + ".json";                
                DateTime blobLastModified;
                //Check if blob exists.
                if (BlobStorageService.IfBlobExists(blobName, out blobLastModified))
                {
                    model.Status = BlobStorageService.GetValueFromBlob(blobName, "Status");
                    model.LastCompletedTime = BlobStorageService.GetValueFromBlob(blobName, "EndTime");
                    model.LastRunDuration = BlobStorageService.GetValueFromBlob(blobName, "RunDuration");
                    model.Notes = BlobStorageService.GetValueFromBlob(blobName, "Message");
                    //check if a trace not older than the frequency exists.
                    if (DateTime.Now.ToLocalTime().Subtract(blobLastModified.ToLocalTime()).TotalMinutes > job.Value)
                    {
                        model.Status = "Warning";
                        model.Notes = string.Format("No trace found for job {0} which is less than {1} minutes old.", job.Key, job.Value);
                    }
                }
                else
                {
                    model.Status = "Error";
                    model.LastCompletedTime = "UnKnown";
                    model.LastRunDuration = "UnKnown";
                    model.Notes = string.Format("Traces not found for the task {0}", job.Key);
                }
                jobsModel.Add(model);
            }
            return jobsModel;
        }
        #endregion

    }
}
