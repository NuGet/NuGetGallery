// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
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
            var result = new Dictionary<int, List<PackageVulnerability>>();
            var packagesMatchingId = _entitiesContext.Packages
                .Where(p => p.PackageRegistration != null && p.PackageRegistration.Id == id)
                .Include($"{nameof(Package.VulnerablePackageRanges)}.{nameof(VulnerablePackageVersionRange.Vulnerability)}");
            foreach (var package in packagesMatchingId)
            {
                if (package.VulnerablePackageRanges == null)
                {
                    continue;
                }

                if (package.VulnerablePackageRanges.Any())
                {
                    result.Add(package.Key,
                        package.VulnerablePackageRanges.Select(vr => vr.Vulnerability).ToList());
                }
            }

            return !result.Any() ? null :
                result.ToDictionary(kv => kv.Key, kv => kv.Value as IReadOnlyList<PackageVulnerability>);
        }

        public bool PackagesContainVulnerability(IQueryable<Package> packages)
        {
            if (packages == null || !packages.Any())
            {
                return false;
            }

            Package first = null;
            foreach (var package in packages)
            {
                if (package.VulnerablePackageRanges != null
                    && package.VulnerablePackageRanges.Any()
                    && package.VulnerablePackageRanges.FirstOrDefault(vpr => vpr.Vulnerability != null) != null)
                {
                    first = package;
                    break;
                }
            }

            return first != null;
        }
    }
}