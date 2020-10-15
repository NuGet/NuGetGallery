// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

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
            var result = new Dictionary<int, List<PackageVulnerability>>();
            foreach (var package in _entitiesContext.Packages.Where(p => p.PackageRegistration != null && p.PackageRegistration.Id == id)
                .Include(p => p.VulnerableVersionRanges).Include("VulnerableVersionRanges.Vulnerability"))
            {
                if (package.VulnerableVersionRanges == null)
                {
                    continue;
                }

                var packageVulnerabilities = (List<PackageVulnerability>)null;
                foreach (var vulnerabilityVersionRange in package.VulnerableVersionRanges)
                {
                    if (packageVulnerabilities == null)
                    {
                        packageVulnerabilities = new List<PackageVulnerability>();
                        result.Add(package.Key, packageVulnerabilities);
                    }

                    packageVulnerabilities.Add(vulnerabilityVersionRange.Vulnerability);
                }
            }

            return result.Count == 0 ? null :
                result.ToDictionary(kv => kv.Key, kv => kv.Value as IReadOnlyList<PackageVulnerability>);
        }
    }
}