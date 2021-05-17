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
        private const int CachingLimitMinutes = 1440; // We could make this 1 day instead (same value) but this is easier for spot testing the cache
        private readonly object Locker = new object();
        private IDictionary<string,
            (DateTime cachedAt, Dictionary<int, IReadOnlyList<PackageVulnerability>> vulnerabilitiesById)> vulnerabilitiesByIdCache
            = new Dictionary<string, (DateTime, Dictionary<int, IReadOnlyList<PackageVulnerability>>)>();

        private readonly IPackageVulnerabilitiesManagementService _packageVulnerabilitiesManagementService;
        public PackageVulnerabilitiesCacheService(IPackageVulnerabilitiesManagementService packageVulnerabilitiesManagementService)
        {
            _packageVulnerabilitiesManagementService = packageVulnerabilitiesManagementService;
            Initialize();
        }

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Must have a value.", nameof(id));
            }

            if (ShouldCachedValueBeUpdated(id))
            {
                lock (Locker)
                {
                    if (ShouldCachedValueBeUpdated(id))
                    {
                        var packageKeyAndVulnerability = _packageVulnerabilitiesManagementService
                            .GetVulnerableRangesById(id)
                            .Include(x => x.Vulnerability)
                            .SelectMany(x => x.Packages.Select(p => new {PackageKey = p.Key, x.Vulnerability}))
                            .GroupBy(pv => pv.PackageKey, pv => pv.Vulnerability)
                            .ToDictionary(pv => pv.Key,
                                pv => pv.ToList().AsReadOnly() as IReadOnlyList<PackageVulnerability>);

                        vulnerabilitiesByIdCache[id] = (cachedAt: DateTime.Now, vulnerabilitiesById: packageKeyAndVulnerability);
                    }
                }
            }

            return vulnerabilitiesByIdCache[id].vulnerabilitiesById.Any()
                ? new ReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>>(vulnerabilitiesByIdCache[id].vulnerabilitiesById)
                : null;
        }

        private void Initialize()
        {
            // We need to build a dictionary of dictionaries. Breaking it down:
            // - this give us a list of all vulnerable package version ranges
            vulnerabilitiesByIdCache = _packageVulnerabilitiesManagementService.GetAllVulnerableRanges()
                .Include(x => x.Vulnerability) 
            // - from these we want a list in this format: (<id>, (<package key>, <vulnerability>))
            //   which will allow us to look up the dictionary by id, and return a dictionary of version -> vulnerability
                .SelectMany(x => x.Packages.Select(p => new
                    {PackageId = x.PackageId ?? string.Empty, KeyVulnerability = new {PackageKey = p.Key, x.Vulnerability}}))
                .GroupBy(ikv => ikv.PackageId, ikv => ikv.KeyVulnerability)
            // - build the outer dictionary, keyed by <id> - each inner dictionary is paired with a time of creation (for cache invalidation)
                .ToDictionary(ikv => ikv.Key,
                    ikv => (cachedAt: DateTime.Now, 
                            vulnerabilitiesById: ikv.GroupBy(kv => kv.PackageKey, kv => kv.Vulnerability)
            // - build the inner dictionaries, all under the same <id>, each keyed by <package key>
                            .ToDictionary(kv => kv.Key,
                                kv => kv.ToList().AsReadOnly() as IReadOnlyList<PackageVulnerability>)));
        }

        private bool ShouldCachedValueBeUpdated(string id) => !vulnerabilitiesByIdCache.ContainsKey(id) ||
                                                              vulnerabilitiesByIdCache[id].cachedAt
                                                                  .AddMinutes(CachingLimitMinutes) < DateTime.Now;
    }
}