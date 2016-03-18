﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGetGallery
{
    public class PackageSearchResults
    {
        public IQueryable<Package> Packages { get; set; }

        public IEnumerable<int> RankedKeys { get; set; }
    }
}