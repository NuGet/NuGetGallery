// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGet.Packaging;

namespace NuGetGallery
{
    public class PackageLicense
        : IEntity
    {
        public int Key { get; set; }

        [Required]
        [StringLength(PackageIdValidator.MaxPackageIdLength)]
        public string Name { get; set; }

        public virtual ICollection<PackageLicenseReport> Reports { get; set; }
    }
}