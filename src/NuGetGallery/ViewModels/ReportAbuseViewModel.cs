// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ReportAbuseViewModel : ReportViewModel
    {
        [Display(Name = "I have already tried to contact the package owner about this problem.")]
        public bool AlreadyContactedOwner { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(4000)]
        [Display(Name = "Your Email Address")]
        [RegularExpression(GalleryConstants.EmailValidationRegex,
            ErrorMessage = "This doesn't appear to be a valid email address.")]
        public string Email { get; set; }

        [StringLength(1000)]
        [Display(Name = "Signature")]
        public string Signature { get; set; }

        [Required(ErrorMessage = "Please enter a message.")]
        [AllowHtml]
        [StringLength(4000)]
        [Display(Name = "Details")]
        public string Message { get; set; }

        public bool ShouldHideReportAbuseForm
        {
            get
            {
                return !IsPackageListed && IsPackageLocked && IsOwnerLocked;
            }
        }
    }
}