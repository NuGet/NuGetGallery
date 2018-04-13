// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class ChangePasswordViewModel
    {
        [Required]
        [Display(Name = "Disable Password Login")]
        public bool DisablePasswordLogin { get; set; }

        [Required]
        [Display(Name = "Current Password")]
        [AllowHtml]
        public string OldPassword { get; set; }

        [Required]
        [Display(Name = "New Password")]
        [PasswordValidation]
        [AllowHtml]
        public string NewPassword { get; set; }

        [Required]
        [Display(Name = "Verify Password")]
        [PasswordValidation]
        [AllowHtml]
        public string VerifyPassword { get; set; }
    }
}