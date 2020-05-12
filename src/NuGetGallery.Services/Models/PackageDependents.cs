// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageDependents
    {
        public IReadOnlyCollection<PackageDependent> PackageList { get; set; }
        public int DependentCount { get; set; }
    }
}