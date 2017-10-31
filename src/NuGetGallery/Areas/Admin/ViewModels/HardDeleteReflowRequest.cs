// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class HardDeleteReflowRequest
    {
        [Required(ErrorMessage = "You must provide an ID for the deleted package to reflow.")]
        public string Id { get; set; }

        [Required(ErrorMessage = "You must provide a version for the deleted package to reflow.")]
        public string Version { get; set; }
    }
}