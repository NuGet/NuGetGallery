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
        private readonly IPackageVulnerabilitiesManagementService _packageVulnerabilitiesManagementService;
        private readonly IPackageVulnerabilitiesCacheService _packageVulnerabilitiesCacheService;

        public PackageVulnerabilitiesService(
            IPackageVulnerabilitiesManagementService packageVulnerabilitiesManagementService,
            IPackageVulnerabilitiesCacheService packageVulnerabilitiesCacheService)
        {
            _packageVulnerabilitiesManagementService = packageVulnerabilitiesManagementService ??
                                                       throw new ArgumentNullException(
                                                           nameof(packageVulnerabilitiesManagementService));
            _packageVulnerabilitiesCacheService = packageVulnerabilitiesCacheService ??
                                                  throw new ArgumentNullException(
                                                      nameof(packageVulnerabilitiesCacheService));
        }

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id) =>
            _packageVulnerabilitiesCacheService.GetVulnerabilitiesById(id, _packageVulnerabilitiesManagementService);

        public bool IsPackageVulnerable(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.VulnerablePackageRanges == null)
            {
                throw new ArgumentException($"{nameof(package.VulnerablePackageRanges)} should be included in package query");
            }

            return package.VulnerablePackageRanges.FirstOrDefault(vpr => vpr.Vulnerability != null) != null;
        }
    }
}