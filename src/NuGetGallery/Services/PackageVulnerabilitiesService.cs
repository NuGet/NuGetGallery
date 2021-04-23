// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageVulnerabilitiesService : IPackageVulnerabilitiesService
    {
        private const int CachingLimitMinutes = 30;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IDictionary<string,
            (DateTime cachedAt, IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> vulnerabilitiesById)> vulnerabilitiesByIdCache 
            = new Dictionary<string, (DateTime, IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>>)>();

        public PackageVulnerabilitiesService(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id)
        {
            if (vulnerabilitiesByIdCache.ContainsKey(id)
                && vulnerabilitiesByIdCache[id].cachedAt.AddMinutes(CachingLimitMinutes) > DateTime.Now)
            {
                return vulnerabilitiesByIdCache[id].vulnerabilitiesById;
            }

            var packageKeyAndVulnerability = _entitiesContext.VulnerableRanges
                .Include(x => x.Vulnerability)
                .Where(x => x.PackageId == id)
                .SelectMany(x => x.Packages.Select(p => new {PackageKey = p.Key, x.Vulnerability}))
                .GroupBy(pv => pv.PackageKey, pv => pv.Vulnerability)
                .ToDictionary(pv => pv.Key, pv => pv.ToList().AsReadOnly() as IReadOnlyList<PackageVulnerability>);

            var result = !packageKeyAndVulnerability.Any() ? null
                : new ReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>>(packageKeyAndVulnerability);

            vulnerabilitiesByIdCache[id] = (cachedAt: DateTime.Now, vulnerabilitiesById: result);

            return result;
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