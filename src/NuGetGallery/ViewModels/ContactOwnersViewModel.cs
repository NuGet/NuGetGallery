// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    [Bind(Include = "Message,CopySender")]
    public class ContactOwnersViewModel
    {
        public string PackageId { get; set; }

        public IEnumerable<User> Owners { get; set; }

        [Display(Name = "Send me a copy")]
        public bool CopySender { get; set; }

        [AllowHtml]
        [Required(ErrorMessage = "Please enter a message.")]
        [StringLength(4000)]
        public string Message { get; set; }
    }
}