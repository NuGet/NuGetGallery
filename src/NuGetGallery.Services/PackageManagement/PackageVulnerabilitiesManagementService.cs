﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class PackageVulnerabilitiesManagementService : IPackageVulnerabilitiesManagementService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageUpdateService _packageUpdateService;
        private readonly ILogger<PackageVulnerabilitiesManagementService> _logger;

        public PackageVulnerabilitiesManagementService(
            IEntitiesContext entitiesContext,
            IPackageUpdateService packageUpdateService,
            ILogger<PackageVulnerabilitiesManagementService> logger)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageUpdateService = packageUpdateService ?? throw new ArgumentNullException(nameof(packageUpdateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ApplyExistingVulnerabilitiesToPackage(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var version = NuGetVersion.Parse(package.NormalizedVersion);
            var possibleRanges = _entitiesContext.VulnerableRanges
                .Where(r => r.PackageId == package.Id)
                .ToList();

            foreach (var possibleRange in possibleRanges)
            {
                var versionRange = VersionRange.Parse(possibleRange.PackageVersionRange);
                if (versionRange.Satisfies(version))
                {
                    package.VulnerablePackageRanges.Add(possibleRange);
                    possibleRange.Packages.Add(package);
                }
            }
        }

        public async Task UpdateVulnerabilityAsync(PackageVulnerability vulnerability, bool withdrawn)
        {
            if (vulnerability == null)
            {
                throw new ArgumentNullException(nameof(vulnerability));
            }

            using (var strategy = new SuspendDbExecutionStrategy())
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                var packagesToUpdate = UpdateVulnerabilityInternal(vulnerability, withdrawn);
                await _entitiesContext.SaveChangesAsync();

                if (packagesToUpdate.Any())
                {
                    await _packageUpdateService.UpdatePackagesAsync(packagesToUpdate.ToList());
                }

                transaction.Commit();
            }
        }

        /// <summary>
        /// Updates the database with <paramref name="vulnerability"/>.
        /// </summary>
        /// <param name="vulnerability">The <see cref="PackageVulnerability"/> to persist in the database.</param>
        /// <param name="withdrawn">Whether or not this vulnerability has been withdrawn.</param>
        /// <param name="packagesToUpdate">The set of <see cref="Package"/>s affected by this operation that should be marked as updated.</param>
        /// <returns>
        /// The set of packages that were updated as part of this operation.
        /// </returns>
        private HashSet<Package> UpdateVulnerabilityInternal(PackageVulnerability vulnerability, bool withdrawn)
        {
            if (vulnerability == null)
            {
                throw new ArgumentNullException(nameof(vulnerability));
            }

            var packagesToUpdate = new HashSet<Package>();

            _logger.LogInformation("Updating vulnerability with GitHub key {GitHubDatabaseKey}", vulnerability.GitHubDatabaseKey);
            // Determine if we already have this vulnerability.
            var existingVulnerability = _entitiesContext.Vulnerabilities
                .Include(v => v.AffectedRanges)
                .Include(v => v.AffectedRanges.Select(pv => pv.Packages))
                .SingleOrDefault(v => v.GitHubDatabaseKey == vulnerability.GitHubDatabaseKey);

            if (existingVulnerability == null)
            {
                _logger.LogInformation("Did not find existing vulnerability with GitHub key {GitHubDatabaseKey}", vulnerability.GitHubDatabaseKey);
                AddNewVulnerability(vulnerability, withdrawn, packagesToUpdate);
            }
            else
            {
                _logger.LogInformation("Found existing vulnerability with GitHub key {GitHubDatabaseKey}", vulnerability.GitHubDatabaseKey);
                UpdateExistingVulnerability(vulnerability, withdrawn, existingVulnerability, packagesToUpdate);
            }

            return packagesToUpdate;
        }

        private void AddNewVulnerability(PackageVulnerability vulnerability, bool withdrawn, HashSet<Package> packagesToUpdate)
        {
            if (withdrawn)
            {
                _logger.LogInformation(
                    "Will not add vulnerability with GitHub key {GitHubDatabaseKey} to database because it is withdrawn", 
                    vulnerability.GitHubDatabaseKey);
                return;
            }

            if (!vulnerability.AffectedRanges.Any())
            {
                // If the vulnerability does not have any vulnerable ranges, it cannot affect any packages.
                // Even if no packages are currently vulnerable to the vulnerability, as long as it has a vulnerable range, 
                // there is at least one package that could be uploaded that would be vulnerable to it.
                _logger.LogInformation(
                    "Will not add vulnerability with GitHub key {GitHubDatabaseKey} to database because it affects no packages",
                    vulnerability.GitHubDatabaseKey);
                return;
            }

            _logger.LogInformation("Adding vulnerability with GitHub key {GitHubDatabaseKey} to database", vulnerability.GitHubDatabaseKey);
            _entitiesContext.Vulnerabilities.Add(vulnerability);
            _entitiesContext.VulnerableRanges.AddRange(vulnerability.AffectedRanges);
            foreach (var newRange in vulnerability.AffectedRanges)
            {
                _logger.LogInformation(
                    "ID {VulnerablePackageId} and version range {VulnerablePackageVersionRange} is now vulnerable to vulnerability with GitHub key {GitHubDatabaseKey}",
                    newRange.PackageId,
                    newRange.PackageVersionRange,
                    vulnerability.GitHubDatabaseKey);

                ProcessNewVulnerabilityRange(newRange, packagesToUpdate);
            }
        }

        private void UpdateExistingVulnerability(PackageVulnerability vulnerability, bool withdrawn, PackageVulnerability existingVulnerability, HashSet<Package> packagesToUpdate)
        {
            // We already have this vulnerability, so we should update it.
            var vulnerablePackages = existingVulnerability.AffectedRanges.SelectMany(pv => pv.Packages);
            if (withdrawn || !vulnerability.AffectedRanges.Any())
            {
                // If the vulnerability was withdrawn or lost all its ranges, all packages marked vulnerable need to be unmarked and updated.
                _logger.LogInformation("Removing vulnerability with GitHub key {GitHubDatabaseKey} from database", vulnerability.GitHubDatabaseKey);
                packagesToUpdate.UnionWith(vulnerablePackages);
                _entitiesContext.Vulnerabilities.Remove(existingVulnerability);
                _entitiesContext.VulnerableRanges.RemoveRange(existingVulnerability.AffectedRanges);
            }
            else
            {
                if (UpdatePackageVulnerabilityMetadata(vulnerability, existingVulnerability))
                {
                    // If the vulnerability's metadata was updated, all packages marked vulnerable need to be updated.
                    _logger.LogInformation("Vulnerability with GitHub key {GitHubDatabaseKey} had its metadata updated", vulnerability.GitHubDatabaseKey);
                    packagesToUpdate.UnionWith(vulnerablePackages);
                }

                UpdateRangesOfPackageVulnerability(vulnerability, existingVulnerability, packagesToUpdate);
            }
        }

        /// <returns>
        /// <c>True</c> when the metadata of the existing vulnerability was changed; otherwise <c>false</c>.
        /// </returns>
        private bool UpdatePackageVulnerabilityMetadata(PackageVulnerability vulnerability, PackageVulnerability existingVulnerability)
        {
            var wasUpdated = false;
            if (vulnerability.Severity != existingVulnerability.Severity)
            {
                existingVulnerability.Severity = vulnerability.Severity;
                wasUpdated = true;
            }

            if (vulnerability.AdvisoryUrl != existingVulnerability.AdvisoryUrl)
            {
                existingVulnerability.AdvisoryUrl = vulnerability.AdvisoryUrl;
                wasUpdated = true;
            }

            return wasUpdated;
        }

        private void UpdateRangesOfPackageVulnerability(PackageVulnerability vulnerability, PackageVulnerability existingVulnerability, HashSet<Package> packagesToUpdate)
        {
            var rangeComparer = new RangeForSameVulnerabilityEqualityComparer();
            // Check for updates in the existing version ranges of this vulnerability.
            foreach (var existingRange in existingVulnerability.AffectedRanges.ToList())
            {
                var updatedRange = vulnerability.AffectedRanges
                    .SingleOrDefault(r => rangeComparer.Equals(existingRange, r));

                if (updatedRange == null)
                {
                    // Any ranges that are missing from the updated vulnerability need to be removed.
                    _logger.LogInformation(
                        "ID {VulnerablePackageId} and version range {VulnerablePackageVersionRange} is no longer vulnerable to vulnerability with GitHub key {GitHubDatabaseKey}",
                        existingRange.PackageId,
                        existingRange.PackageVersionRange,
                        vulnerability.GitHubDatabaseKey);

                    _entitiesContext.VulnerableRanges.Remove(existingRange);
                    existingVulnerability.AffectedRanges.Remove(existingRange);
                    packagesToUpdate.UnionWith(existingRange.Packages);
                }
                else
                {
                    // Any range that had its first patched version updated needs to be updated.
                    if (existingRange.FirstPatchedPackageVersion != updatedRange.FirstPatchedPackageVersion)
                    {
                        existingRange.FirstPatchedPackageVersion = updatedRange.FirstPatchedPackageVersion;
                        packagesToUpdate.UnionWith(existingRange.Packages);
                    }
                }
            }

            // Any new ranges in the updated vulnerability need to be added to the database.
            var newRanges = vulnerability.AffectedRanges
                .Except(existingVulnerability.AffectedRanges, rangeComparer)
                .ToList();
            foreach (var newRange in newRanges)
            {
                _logger.LogInformation(
                    "ID {VulnerablePackageId} and version range {VulnerablePackageVersionRange} is now vulnerable to vulnerability with GitHub key {GitHubDatabaseKey}",
                    newRange.PackageId,
                    newRange.PackageVersionRange,
                    vulnerability.GitHubDatabaseKey);

                newRange.Vulnerability = existingVulnerability; // this needs to happen before we update _entitiesContext, otherwise index uniqueness conflicts occur
                _entitiesContext.VulnerableRanges.Add(newRange);
                existingVulnerability.AffectedRanges.Add(newRange);
                ProcessNewVulnerabilityRange(newRange, packagesToUpdate);
            }
        }

        /// <summary>
        /// Iterates through the <see cref="Package"/>s that could be vulnerable to <paramref name="range"/>.
        /// If any of these packages have not been marked vulnerable and should be, mark them vulnerable and add them to <paramref name="packagesToUpdate"/>.
        /// </summary>
        /// <remarks>
        /// It is not possible to query the database to only return packages satisfying the version range, so we must iterate through all <see cref="Package"/>s in the <see cref="PackageRegistration"/>.
        /// </remarks>
        private void ProcessNewVulnerabilityRange(VulnerablePackageVersionRange range, HashSet<Package> packagesToUpdate)
        {
            _logger.LogInformation(
                "Marking packages that match ID {VulnerablePackageId} and version range {VulnerablePackageVersionRange} vulnerable",
                range.PackageId,
                range.PackageVersionRange);

            var versionRange = VersionRange.Parse(range.PackageVersionRange);
            var packages = _entitiesContext.PackageRegistrations
                .Where(pr => pr.Id == range.PackageId)
                .SelectMany(pr => pr.Packages)
                .ToList();

            foreach (var package in packages)
            {
                var version = NuGetVersion.Parse(package.NormalizedVersion);
                var satisfiesVersionRange = versionRange.Satisfies(version);
                if (satisfiesVersionRange)
                {
                    _logger.LogInformation(
                        "Package with ID {VulnerablePackageId} and version {VulnerablePackageVersion} satisfies {VulnerablePackageVersionRange} and is vulnerable",
                        range.PackageId,
                        package.NormalizedVersion,
                        range.PackageVersionRange);

                    package.VulnerablePackageRanges.Add(range);
                    range.Packages.Add(package);
                    packagesToUpdate.Add(package);
                }
            }
        }

        /// <remarks>
        /// A <see cref="VulnerablePackageVersionRange"/> is equal to another <see cref="VulnerablePackageVersionRange"/> if it has the same
        /// <see cref="PackageVulnerability"/>, <see cref="VulnerablePackageVersionRange.PackageId"/>, and <see cref="VulnerablePackageVersionRange.PackageVersionRange"/>.
        /// We have determined that the <see cref="PackageVulnerability"/> is the same already, so no need to compare it.
        /// </remarks>
        private class RangeForSameVulnerabilityEqualityComparer : IEqualityComparer<VulnerablePackageVersionRange>
        {
            public bool Equals(VulnerablePackageVersionRange x, VulnerablePackageVersionRange y)
            {
                return x?.PackageId == y?.PackageId
                    && x?.PackageVersionRange == y?.PackageVersionRange;
            }

            public int GetHashCode(VulnerablePackageVersionRange obj)
            {
                return Tuple
                    .Create(
                        obj?.PackageId,
                        obj?.PackageVersionRange)
                    .GetHashCode();
            }
        }
    }
}
