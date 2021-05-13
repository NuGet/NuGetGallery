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
    public class PackageVulnerabilitiesCacheService : IPackageVulnerabilitiesCacheService
    {
        private const int CachingLimitMinutes = 30;
        private readonly object Locker = new object();
        private readonly IDictionary<string,
            (DateTime cachedAt, IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> vulnerabilitiesById)> vulnerabilitiesByIdCache
            = new Dictionary<string, (DateTime, IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>>)>();

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id,
            IPackageVulnerabilitiesManagementService packageVulnerabilitiesManagementService)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Must have a value.", nameof(id));
            }
            if (packageVulnerabilitiesManagementService == null)
            {
                throw new ArgumentNullException(nameof(packageVulnerabilitiesManagementService));
            }

            if (ShouldCachedValueBeUpdated(id))
            {
                lock (Locker)
                {
                    if (ShouldCachedValueBeUpdated(id))
                    {
                        var packageKeyAndVulnerability = packageVulnerabilitiesManagementService
                            .GetVulnerableRangesById(id)
                            .Include(x => x.Vulnerability)
                            .Where(x => x.PackageId == id)
                            .SelectMany(x => x.Packages.Select(p => new {PackageKey = p.Key, x.Vulnerability}))
                            .GroupBy(pv => pv.PackageKey, pv => pv.Vulnerability)
                            .ToDictionary(pv => pv.Key,
                                pv => pv.ToList().AsReadOnly() as IReadOnlyList<PackageVulnerability>);

                        var result = !packageKeyAndVulnerability.Any()
                            ? null
                            : new ReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>>(
                                packageKeyAndVulnerability);

                        vulnerabilitiesByIdCache[id] = (cachedAt: DateTime.Now, vulnerabilitiesById: result);
                    }
                }
            }

            return vulnerabilitiesByIdCache[id].vulnerabilitiesById;
        }

        private bool ShouldCachedValueBeUpdated(string id) => !vulnerabilitiesByIdCache.ContainsKey(id) ||
                                                              vulnerabilitiesByIdCache[id].cachedAt
                                                                  .AddMinutes(CachingLimitMinutes) < DateTime.Now;
    }
}