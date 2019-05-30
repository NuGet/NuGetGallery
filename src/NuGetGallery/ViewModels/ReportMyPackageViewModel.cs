// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ReportMyPackageViewModel : ReportViewModel
    {
        public bool AllowDelete { get; set; }
        
        public PackageDeleteDecision? DeleteDecision { get; set; }

        public bool DeleteConfirmation { get; set; }
                
        [AllowHtml]
        [StringLength(4000)]
        [Display(Name = "Details")]
        public string Message { get; set; }

        public IReadOnlyList<ReportPackageReason> DeleteReasonChoices { get; set; }
    }
}