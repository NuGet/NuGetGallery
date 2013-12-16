using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetDashboard.Models
{
    public class WorkerJobStatusViewModel
    {
        public WorkerJobStatusViewModel(string name, string status, string lastCompletedTime, string lastRunDuration, string notes)
        {
                this.Name = name;
                this.Status = status;
                this.LastCompletedTime = lastCompletedTime;
                this.LastRunDuration = lastRunDuration;
                this.Notes = notes;
        }

        public WorkerJobStatusViewModel(string name)
        {
            this.Name = name;          
        }

            public string Name;
            public string Status;
            public string LastCompletedTime;
            public string LastRunDuration;
            public string Notes;

    }
}