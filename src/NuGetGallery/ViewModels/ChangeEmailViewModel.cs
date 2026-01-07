// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ChangeEmailViewModel
    {
        [Required]
        [StringLength(255)]
        [Display(Name = "New Email Address")]
        [RegularExpression(GalleryConstants.EmailValidationRegex, ErrorMessage = GalleryConstants.EmailValidationErrorMessage)]
        public string NewEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [StringLength(64)]
        [AllowHtml]
        public string Password { get; set; }
    }
}
