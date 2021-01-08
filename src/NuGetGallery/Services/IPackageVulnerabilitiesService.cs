// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageVulnerabilitiesService
    {
        /// <summary>
        /// Returns a dictionary mapping package keys to collections of vulnerabilities for that package/version
        /// </summary>
        /// <param name="id">id of the package for this query</param>
        IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id);

        /// <summary>
        /// Returns true if any package in the collection has a vulnerability
        /// </summary>
        /// <param name="packages">collection of packages to examine</param>
        bool PackagesContainVulnerability(IQueryable<Package> packages);
    }
}