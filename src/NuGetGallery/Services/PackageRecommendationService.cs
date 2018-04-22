// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageRecommendationService : IPackageRecommendationService
    {
        public PackageRecommendationService()
        {
        }

        public async Task<IEnumerable<RecommendedPackageViewModel>> GetRecommendedPackagesAsync(Package package)
        {
            // TODO
        }
    }
}
