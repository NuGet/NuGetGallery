// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IPackageRecommendationService
    {
        /// <summary>
        /// Gets a list of alternative packages recommended for users browsing the package.
        /// </summary>
        Task<IEnumerable<ListPackageItemViewModel>> GetRecommendedPackagesAsync(Package package);
    }
}
