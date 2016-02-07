// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class HistoryViewModel
    {
        public List<HistoryListModel> HistoryList;
        public int CurrentAssignedToFilter { get; set; }
        public int CurrentIssueStatusNameFilter { get; set; }
        public string CurrentReasonFilter { get; set; }
        public int CurrentPageNumber { get; set; }
        public int CurrentStatusId { get; set; }
    }

    public class HistoryListModel
    {
        public History History { get; set; }
        public string EditedBy { get; set; }
    }
}