// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery.ViewModels
{
    public class ContactSupportViewModel
    {
        [Required(ErrorMessage ="Please enter a subject line.")]
        [StringLength(100)]
        [Display(Name ="Subject Line")]
        [AllowHtml]
        public string SubjectLine { get; set; }

        [Required(ErrorMessage ="Please enter a message.")]
        [StringLength(4000)]
        [Display(Name ="Message for support")]
        [AllowHtml]
        public string Message { get; set; }

        [Display(Name = "Send me a copy")]
        public bool CopySender { get; set; }
    }
}