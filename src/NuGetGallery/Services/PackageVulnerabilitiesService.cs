// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageVulnerabilitiesService : IPackageVulnerabilitiesService
    {
        private readonly IPackageVulnerabilitiesCacheService _packageVulnerabilitiesCacheService;

        public PackageVulnerabilitiesService(IPackageVulnerabilitiesCacheService packageVulnerabilitiesCacheService)
        {
            _packageVulnerabilitiesCacheService = packageVulnerabilitiesCacheService ??
                                                  throw new ArgumentNullException(
                                                      nameof(packageVulnerabilitiesCacheService));
        }

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id) =>
            _packageVulnerabilitiesCacheService.GetVulnerabilitiesById(id);

        public bool IsPackageVulnerable(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return GetVulnerabilitiesById(package.PackageRegistration.Id)?.Where(p => p.Key == package.Key).Any() ?? false;
        }
    }
}