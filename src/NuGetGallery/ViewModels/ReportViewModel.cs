// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public abstract class ReportViewModel : IPackageVersionModel
    {
        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        [Display(Name = "Reason")]
        [Required(ErrorMessage = "You must select a reason for reporting the package.")]
        public ReportPackageReason? Reason { get; set; }

        [Display(Name = "Send me a copy")]
        public bool CopySender { get; set; }

        public bool ConfirmedUser { get; set; }

        public IReadOnlyList<ReportPackageReason> ReasonChoices { get; set; }

        public string Id => PackageId;
        public string Version => PackageVersion;
    }
}