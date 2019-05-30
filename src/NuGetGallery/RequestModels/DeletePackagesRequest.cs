// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class DeletePackagesRequest
    {
        public DeletePackagesRequest()
        {
            Packages = new List<string>();
            SoftDelete = true;
        }

        public List<string> Packages { get; set; }

        [NotEqual(ReportPackageReason.HasABugOrFailedToInstall, ErrorMessage = "Unfortunately we cannot provide support for bugs in NuGet Packages. Please contact owner(s) for assistance.")]
        [Required(ErrorMessage = "You must select a reason for deleting the package")]
        [Display(Name = "Reason")]
        public ReportPackageReason? Reason { get; set; }

        [Required(ErrorMessage = "Please sign using your name.")]
        [StringLength(1000)]
        [Display(Name = "Signature")]
        public string Signature { get; set; }

        [Display(Name = "Keep the package ID and version reserved")]
        public bool SoftDelete { get; set; }

        [Display(Name = "Remove the package registration when all packages are deleted")]
        public bool DeleteEmptyPackageRegistration { get; set; }

        public IEnumerable<ReportPackageReason> ReasonChoices { get; set; }
    }
}
