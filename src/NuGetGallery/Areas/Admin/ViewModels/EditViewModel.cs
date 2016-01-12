using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Areas.Admin.Models;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class EditViewModel
    {
        public Issue Issue { get; set; }
        public int CurrentAssignedToFilter { get; set; }
        public int CurrentIssueStatusNameFilter { get; set; }
        public string CurrentReasonFilter { get; set; }
        public int CurrentPageNumber { get; set; }
        public int CurrentStatusId { get; set; }
        public string AssignedToLabel { get; set; }
        public string IssueStatusNameLabel { get; set; }
    }
}