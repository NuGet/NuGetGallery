// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class ReportAbuseViewModel : ReportMyPackageViewModel
    {
        [Display(Name = "I have already tried to contact the package owner about this problem.")]
        public bool AlreadyContactedOwner { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [StringLength(4000)]
        [Display(Name = "Your Email Address")]
        //[DataType(DataType.EmailAddress)] - does not work with client side validation
        [RegularExpression(RegisterViewModel.EmailValidationRegex,
            ErrorMessage = "This doesn't appear to be a valid email address.")]
        public string Email { get; set; }

        [StringLength(1000)]
        [Display(Name = "Signature")]
        public string Signature { get; set; }

        public ReportAbuseViewModel()
        {
        }
    }
}