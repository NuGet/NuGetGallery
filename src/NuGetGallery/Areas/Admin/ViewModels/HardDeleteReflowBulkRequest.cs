// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class HardDeleteReflowBulkRequest
    {
        [Display(Name = "List")]
        [Required(ErrorMessage = "You must provide a list of deleted packages to reflow.")]
        public string BulkList { get; set; }
    }
}