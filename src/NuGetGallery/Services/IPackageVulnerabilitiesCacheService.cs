// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// This interface is used to implement a basic caching for vulnerabilities querying.
    /// /// </summary>
    public interface IPackageVulnerabilitiesCacheService
    {
        /// <summary>
        /// This function is used to get the packages by id dictionary from the cache
        /// </summary>
        IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id);
    }
}
