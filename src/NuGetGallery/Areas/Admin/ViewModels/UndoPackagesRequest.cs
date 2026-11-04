// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class UndoPackagesRequest
    {
        public List<UndoPackageRequest> Packages { get; set; } = new List<UndoPackageRequest>();

        [Required]
        [StringLength(1000)]
        public string Reason { get; set; }
    }
}