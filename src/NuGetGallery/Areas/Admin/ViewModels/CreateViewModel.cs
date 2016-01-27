// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Areas.Admin.Models;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class CreateViewModel
    {
        public List<SelectListItem> AssignedToChoices { get; set; }
        public List<SelectListItem> IssueStatusNameChoices { get; set; }
        public List<SelectListItem> ReasonChoices { get; set; }
        public Issue Issue { get; set; }
    }
}