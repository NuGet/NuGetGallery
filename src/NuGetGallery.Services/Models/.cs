// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Services.Models
{
    class PackageDependents
    {
        public IReadOnlyCollection<PackageDependent> PackageList { get; set; }
        public int DependentCount { get; set; }
    }
}