// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class CreateCuratedPackageRequest
    {
        [Required]
        [DisplayName("Package ID")]
        public string PackageId { get; set; }

        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }
    }
}