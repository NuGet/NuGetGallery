// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class PackageLicense
    {
        public int Key { get; set; }

        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        public virtual ICollection<PackageLicenseReport> Reports { get; set; }
    }
}