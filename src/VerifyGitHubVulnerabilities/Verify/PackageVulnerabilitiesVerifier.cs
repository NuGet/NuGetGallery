// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using VerifyGitHubVulnerabilities.Configuration;
using PackageVulnerabilitySeverity = NuGet.Services.Entities.PackageVulnerabilitySeverity;

namespace VerifyGitHubVulnerabilities.Verify
{
    public class PackageVulnerabilitiesVerifier : IPackageVulnerabilitiesVerifier
    {
        private readonly VerifyGitHubVulnerabilitiesConfiguration _configuration;
        private readonly IEntitiesContext _entitiesContext;
        private readonly ILogger<PackageVulnerabilitiesVerifier> _logger;

        private Lazy<Task<PackageMetadataResource>> _packageMetadataResource;
        private Dictionary<string, IEnumerable<IPackageSearchMetadata>> _packageMetadata;

        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

        public PackageVulnerabilitiesVerifier(
            VerifyGitHubVulnerabilitiesConfiguration configuration,
            IEntitiesContext entitiesContext,
            ILogger<PackageVulnerabilitiesVerifier> logger)
        {
            _configuration = configuration;
            if (_configuration.VerifyDatabase)
            {
                _entitiesContext = entitiesContext;
            }

            _packageMetadata = new Dictionary<string, IEnumerable<IPackageSearchMetadata>>();
            _packageMetadataResource = new Lazy<Task<PackageMetadataResource>>(InitializeMetadataResourceAsync);
            _logger = logger;
        }

        public bool HasErrors { get; private set; }

        public void ApplyExistingVulnerabilitiesToPackage(Package package)
        {
            throw new NotImplementedException();
        }

        public Task UpdateVulnerabilityAsync(PackageVulnerability vulnerability, bool withdrawn)
        {
            if (vulnerability == null)
            {
                _logger.LogWarning("Null vulnerability passed to verifier! Continuing...");
                return Task.CompletedTask;
            }

            if (_configuration.VerifyDatabase)
            {
                VerifyVulnerabilityInDatabase(vulnerability, withdrawn);
            }

            // Note: testing a withdrawn advisory isn't practical in registration metadata. We can only download
            // metadata for a package, and would need to download all package/version blobs to determine an advisory
            // is no longer present. Covering withdrawn advisory processing in the database will be adequate.
            if (_configuration.VerifyRegistrationMetadata && !withdrawn)
            {
                return VerifyVulnerabilityInMetadataAsync(vulnerability);
            }

            return Task.CompletedTask;
        }

        private void VerifyVulnerabilityInDatabase(PackageVulnerability vulnerability, bool withdrawn)
        {
            _logger.LogInformation(
                "[Database] Verifying vulnerability {GitHubDatabaseKey} (advisory URL: {AdvisoryUrl}).",
                vulnerability.GitHubDatabaseKey,
                vulnerability.AdvisoryUrl);

            var existingVulnerability = _entitiesContext.Vulnerabilities
                .Include(v => v.AffectedRanges)
                .SingleOrDefault(v => v.GitHubDatabaseKey == vulnerability.GitHubDatabaseKey);

            if (withdrawn || !vulnerability.AffectedRanges.Any())
            {
                if (existingVulnerability != null)
                {
                    _logger.LogError(withdrawn ?
                        "[Database] Vulnerability advisory {GitHubDatabaseKey} was withdrawn and should not be in DB!" :
                        "[Database] Vulnerability advisory {GitHubDatabaseKey} affects no packages and should not be in DB!", vulnerability.GitHubDatabaseKey);
                    HasErrors = true;
                }

                return;
            }

            if (existingVulnerability == null)
            {
                _logger.LogError("[Database] Cannot find vulnerability {GitHubDatabaseKey} in DB!", vulnerability.GitHubDatabaseKey);
                HasErrors = true;
                return;
            }

            if (existingVulnerability.Severity != vulnerability.Severity)
            {
                _logger.LogError(
                    "[Database] Vulnerability advisory {GitHubDatabaseKey}, severity does not match! GitHub: {GitHubSeverity}, DB: {DbSeverity}",
                    vulnerability.GitHubDatabaseKey,
                    vulnerability.Severity,
                    existingVulnerability.Severity);
                HasErrors = true;
            }

            if (existingVulnerability.AdvisoryUrl != vulnerability.AdvisoryUrl)
            {
                _logger.LogError(
                    "[Database] Vulnerability advisory {GitHubDatabaseKey}, advisory URL does not match! GitHub: {GitHubAdvisoryUrl}, DB: {DbAdvisoryUrl}",
                    vulnerability.GitHubDatabaseKey,
                    vulnerability.AdvisoryUrl,
                    existingVulnerability.AdvisoryUrl);
                HasErrors = true;
            }

            foreach (var range in vulnerability.AffectedRanges)
            {
                _logger.LogInformation("[Database] Verifying range affecting {PackageId} {PackageVersionRange}.", range.PackageId, range.PackageVersionRange);
                var existingRange = existingVulnerability.AffectedRanges
                    .SingleOrDefault(r => r.PackageId == range.PackageId && r.PackageVersionRange == range.PackageVersionRange);

                if (existingRange == null)
                {
                    _logger.LogError(
                        "[Database] Vulnerability advisory {GitHubDatabaseKey}, cannot find range {PackageId} {PackageVersionRange} in DB!",
                        vulnerability.GitHubDatabaseKey,
                        range.PackageId,
                        range.PackageVersionRange);
                    HasErrors = true;
                    continue;
                }

                if (existingRange.FirstPatchedPackageVersion != range.FirstPatchedPackageVersion)
                {
                    _logger.LogError(
                        "[Database] Vulnerability advisory {GitHubDatabaseKey}, range {PackageId} {PackageVersionRange}, " +
                        "first patched version does not match! GitHub: {GitHubFirstPatchedPackageVersion}, DB: {DbFirstPatchedPackageVersion}",
                        vulnerability.GitHubDatabaseKey,
                        range.PackageVersionRange,
                        range.PackageVersionRange,
                        range.FirstPatchedPackageVersion,
                        existingRange.FirstPatchedPackageVersion);
                    HasErrors = true;
                }

                var packages = _entitiesContext.Packages
                    .Where(p => p.PackageRegistration.Id == range.PackageId)
                    .Include(p => p.VulnerablePackageRanges)
                    .ToList();

                var versionRange = VersionRange.Parse(range.PackageVersionRange);
                foreach (var package in packages)
                {
                    var version = NuGetVersion.Parse(package.NormalizedVersion);
                    if (versionRange.Satisfies(version) != package.VulnerablePackageRanges.Contains(existingRange))
                    {
                        _logger.LogError(
                            "[Database] Vulnerability advisory {GitHubDatabaseKey}, " +
                            "range {PackageId} {PackageVersionRange}, package {NormalizedVersion} is not properly marked vulnerable to vulnerability!",
                            vulnerability.GitHubDatabaseKey,
                            range.PackageId,
                            range.PackageVersionRange,
                            package.NormalizedVersion);
                        HasErrors = true;
                    }
                }
            }
        }

        private Task VerifyVulnerabilityInMetadataAsync(PackageVulnerability gitHubAdvisory)
        {
            _logger.LogInformation(
                "[Metadata] Verifying vulnerability {GitHubDatabaseKey} (advisory URL: {AdvisoryUrl}).",
                gitHubAdvisory.GitHubDatabaseKey,
                gitHubAdvisory.AdvisoryUrl);

            if (gitHubAdvisory.AffectedRanges == null || !gitHubAdvisory.AffectedRanges.Any())
            {
                return Task.CompletedTask;
            }

            // Group ranges by id -- this makes testing metadata collections cleaner
            var rangesById = new Dictionary<string, IList<string>>();
            foreach (var range in gitHubAdvisory.AffectedRanges)
            {
                var id = range.PackageId.Trim(' '); // some incoming data needs cleaning
                if (rangesById.TryGetValue(id, out var packageVersionRangeForId))
                {
                    packageVersionRangeForId.Add(range.PackageVersionRange);
                }
                else
                {
                    rangesById[id] = new List<string> {range.PackageVersionRange};
                }
            }

            var verificationJobsForAdvisory = new List<Task>();
            foreach (var rangeById in rangesById)
            {
                verificationJobsForAdvisory.Add(
                    VerifyVulnerabilityForRangeAsync(
                        rangeById.Key,
                        ranges: rangeById.Value,
                        gitHubAdvisory.AdvisoryUrl,
                        gitHubAdvisory.GitHubDatabaseKey,
                        gitHubAdvisory.Severity)
                );
            }

            return Task.WhenAll(verificationJobsForAdvisory);
        }

        private async Task VerifyVulnerabilityForRangeAsync(
            string packageId,
            IList<string> ranges,
            string advisoryUrl, 
            int advisoryDatabaseKey, 
            PackageVulnerabilitySeverity advisorySeverity)
        {
            // Fetch metadata from registration blobs for verification--a collection of all versions of the package Id
            var metadata = await GetPackageMetadataAsync(packageId);
            foreach (var versionMetadata in metadata)
            {
                var matchingVulnerabilities = Enumerable.Empty<PackageVulnerabilityMetadata>();
                if (versionMetadata.Vulnerabilities != null)
                {
                    matchingVulnerabilities = versionMetadata.Vulnerabilities.Where(v => v.AdvisoryUrl.ToString() == advisoryUrl);
                }

                var hasTheVulnerability = matchingVulnerabilities.Any();

                // Check whether a version range pertaining to this id in the github advisory is satisfied by this metadata version
                var versionisInGitHubRange = false;
                foreach (var range in ranges)
                {
                    var gitHubVersionRange = VersionRange.Parse(range);
                    if (gitHubVersionRange.Satisfies(versionMetadata.Identity.Version, new VersionComparer()))
                    {
                        versionisInGitHubRange = true;
                        break;
                    }
                }

                if (versionisInGitHubRange)
                {
                    if (!hasTheVulnerability)
                    {
                        _logger.LogError(
                            "[Metadata] Vulnerability advisory {AdvisoryDatabaseKey}, version {Version} of package {PackageId} " +
                            "is not marked vulnerable and is in a vulnerable range!",
                            advisoryDatabaseKey,
                            versionMetadata.Identity.Version,
                            packageId);
                        HasErrors = true;
                    }

                    // Test whether we have any severity mismatches
                    var firstSeverityMismatch = matchingVulnerabilities
                        .FirstOrDefault(v => v.Severity != (int)advisorySeverity);
                    if (firstSeverityMismatch != null)
                    {
                        _logger.LogError(
                            "[Metadata] Vulnerability advisory {AdvisoryDatabaseKey}, severities has at least one mismatch! " +
                            "GitHub: {GitHubAdvisorySeverity}, Metadata: {FirstSeverityMismatchSeverity}",
                            advisoryDatabaseKey,
                            advisorySeverity,
                            firstSeverityMismatch.Severity);
                        HasErrors = true;
                    }
                }
                else
                {
                    if (hasTheVulnerability)
                    {
                        _logger.LogError(
                            "[Metadata] Vulnerability advisory {AdvisoryDatabaseKey}, version {Version} of package {PackageId} " +
                            "is marked vulnerable and is not in a vulnerable range!",
                            advisoryDatabaseKey,
                            versionMetadata.Identity.Version,
                            packageId);
                        HasErrors = true;
                    }
                }
            }
        }

        private async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadataAsync(string packageId)
        {
            // We need this to be thread-safe as it's called by multiple tasks concurrently
            await semaphoreSlim.WaitAsync();

            try
            {
                if (!_packageMetadata.TryGetValue(packageId, out IEnumerable<IPackageSearchMetadata> metadata))
                {
                    using (var cacheContext = new SourceCacheContext())
                    {
                        cacheContext.NoCache = true;
                        metadata = (await (await _packageMetadataResource.Value).GetMetadataAsync(
                            packageId,
                            includePrerelease: true,
                            includeUnlisted: true,
                            sourceCacheContext: cacheContext,
                            log: NuGet.Common.NullLogger.Instance,
                            token: CancellationToken.None)).ToList();
                        _packageMetadata[packageId] = metadata;
                    }
                }

                return metadata;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private async Task<PackageMetadataResource> InitializeMetadataResourceAsync()
        {
            var providers = Repository.Provider.GetCoreV3();
            var packageSource = new PackageSource(_configuration.NuGetV3Index, "NuGet Source", isEnabled: true);
            var sourceRepository = Repository.CreateSource(providers, packageSource, FeedType.Undefined);
            return await sourceRepository.GetResourceAsync<PackageMetadataResource>(CancellationToken.None);
        }
    }
}
