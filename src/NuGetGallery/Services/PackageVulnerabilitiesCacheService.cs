// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageVulnerabilitiesCacheService : IPackageVulnerabilitiesCacheService
    {
        private IDictionary<string,
            Dictionary<int, IReadOnlyList<PackageVulnerability>>> _vulnerabilitiesByIdCache
            = new ConcurrentDictionary<string, Dictionary<int, IReadOnlyList<PackageVulnerability>>>();
        private readonly object _refreshLock = new object();
        private bool _isRefreshing;

        private readonly IPackageVulnerabilitiesManagementService _packageVulnerabilitiesManagementService;
        private readonly ITelemetryService _telemetryService;

        public PackageVulnerabilitiesCacheService(
            IPackageVulnerabilitiesManagementService packageVulnerabilitiesManagementService,
            ITelemetryService telemetryService)
        {
            _packageVulnerabilitiesManagementService = packageVulnerabilitiesManagementService;
            _telemetryService = telemetryService;
        }

        public IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Must have a value.", nameof(id));
            }

            if (_vulnerabilitiesByIdCache.TryGetValue(id, out var result))
            {
                return result;
            }

            return null;
        }

        public void RefreshCache()
        {
            bool shouldRefresh = false;
            lock (_refreshLock)
            {
                if (!_isRefreshing)
                {
                    _isRefreshing = true;
                    shouldRefresh = true;
                }
            }

            if (shouldRefresh)
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    // We need to build a dictionary of dictionaries. Breaking it down:
                    // - this give us a list of all vulnerable package version ranges
                    _vulnerabilitiesByIdCache = _packageVulnerabilitiesManagementService.GetAllVulnerableRanges()
                        .Include(x => x.Vulnerability)
                        // - from these we want a list in this format: (<id>, (<package key>, <vulnerability>))
                        //   which will allow us to look up the dictionary by id, and return a dictionary of version -> vulnerability
                        .SelectMany(x => x.Packages.Select(p => new
                            { PackageId = x.PackageId ?? string.Empty, KeyVulnerability = new { PackageKey = p.Key, x.Vulnerability } }))
                        .GroupBy(ikv => ikv.PackageId, ikv => ikv.KeyVulnerability)
                        // - build the outer dictionary, keyed by <id> - each inner dictionary is paired with a time of creation (for cache invalidation)
                        .ToDictionary(ikv => ikv.Key,
                            ikv =>
                                ikv.GroupBy(kv => kv.PackageKey, kv => kv.Vulnerability)
                                    // - build the inner dictionaries, all under the same <id>, each keyed by <package key>
                                    .ToDictionary(kv => kv.Key,
                                        kv => kv.ToList().AsReadOnly() as IReadOnlyList<PackageVulnerability>));
                    
                    stopwatch.Stop();
                    
                    _telemetryService.TrackVulnerabilitiesCacheRefreshDuration(stopwatch.ElapsedMilliseconds);
                }
                finally
                {
                    _isRefreshing = false;
                }
            }
        }
    }
}