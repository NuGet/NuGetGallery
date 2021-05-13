// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageVulnerabilitiesManagementService
    {
        /// <summary>
        /// Adds any <see cref="VulnerablePackageVersionRange"/>s to <see cref="Package.VulnerableVersionRanges"/> that it is a part of.
        /// </summary>
        /// <remarks>
        /// Does not commit changes. The caller is expected to commit any changes separately.
        /// </remarks>
        void ApplyExistingVulnerabilitiesToPackage(Package package);

        /// <summary>
        /// If we don't currently have <paramref name="vulnerability"/> in our database, adds it and its <see cref="PackageVulnerability.AffectedRanges"/>.
        /// If we do, updates the existing entity.
        /// </summary>
        /// <param name="withdrawn">Whether or not the vulnerability was withdrawn.</param>
        Task UpdateVulnerabilityAsync(PackageVulnerability vulnerability, bool withdrawn);

        /// <summary>
        /// Get a package's collection of vulnerable ranges.
        /// </summary>
        /// <param name="packageId">The package's Id</param>
        /// <returns>The package's vulnerable ranges, connecting it to <see cref="PackageVulnerability" /> instances</returns>
        IQueryable<VulnerablePackageVersionRange> GetVulnerableRangesById(string packageId);
    }
}