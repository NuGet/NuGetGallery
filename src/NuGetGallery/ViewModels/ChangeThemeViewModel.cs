// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ChangeThemeViewModel
    {
        [Required]
        [StringLength(64)]
        [AllowHtml]
        public string Theme { get; set; }
    }
}
