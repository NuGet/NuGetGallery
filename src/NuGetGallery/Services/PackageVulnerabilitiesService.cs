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
        private readonly IEntitiesContext _entitiesContext;

        public PackageVulnerabilitiesService(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id)
        {

            var packageKeyAndVulnerability = _entitiesContext.VulnerableRanges
                .Include(x => x.Vulnerability)
                .Where(x => x.PackageId == id)
                .SelectMany(x => x.Packages.Select(p => new { PackageKey = p.Key, x.Vulnerability }))
                .ToList();

            return !packageKeyAndVulnerability.Any() ? null :
                packageKeyAndVulnerability.ToDictionary(kv => kv.PackageKey, kv => kv.Vulnerability as IReadOnlyList<PackageVulnerability>);
        }

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