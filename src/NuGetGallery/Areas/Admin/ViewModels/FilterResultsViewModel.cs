using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Areas.Admin.Models;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FilterResultsViewModel
    {
        public List<SelectListItem> AssignedToChoices { get; set; }
        public List<SelectListItem> IssueStatusNameChoices { get; set; }
        public List<SelectListItem> ReasonChoices { get; set; }
        public ReportPackageReason? Reason { get; set; }
        public int StatusID { get; set; }
        public int PageNumber { get; set; }
        public int? AssignedTo { get; set; }
        public int? IssueStatusName { get; set; }
    }
}