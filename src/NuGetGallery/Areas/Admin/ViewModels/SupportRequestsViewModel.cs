// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class SupportRequestsViewModel
    {
        public SupportRequestsViewModel()
        {
            Issues = new List<SupportRequestViewModel>();
        }

        public List<SupportRequestViewModel> Issues { get; set; }
        public List<SelectListItem> AssignedToChoices { get; set; }
        public List<SelectListItem> IssueStatusNameChoices { get; set; }
        public List<SelectListItem> ReasonChoices { get; set; }

        public int MaxPage { get; set; }
        public int CurrentPageNumber { get; set; }
        public int ItemsPerPage { get; set; }

        public string ReasonFilter { get; set; }
        public int? AssignedToFilter { get; set; }
        public int? IssueStatusIdFilter { get; set; }
    }
}