// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureFlagsViewModel
    {
        [Required]
        public string Flags { get; set; }

        [Required]
        public string ContentId { get; set; }
    }
}