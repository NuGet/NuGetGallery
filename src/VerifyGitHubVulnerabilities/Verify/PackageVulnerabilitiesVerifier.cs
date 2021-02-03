// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using VerifyGitHubVulnerabilities.Configuration;

namespace VerifyGitHubVulnerabilities.Verify
{
    public class PackageVulnerabilitiesVerifier : IPackageVulnerabilitiesManagementService
    {
        private readonly VerifyGitHubVulnerabilitiesConfiguration _configuration;
        private readonly IEntitiesContext _entitiesContext;

        private PackageMetadataResource _packageMetadataResource;
        private Dictionary<string, IEnumerable<IPackageSearchMetadata>> _packageMetadata;

        public PackageVulnerabilitiesVerifier(IServiceProvider serviceProvider)
        {
            _configuration = serviceProvider.GetRequiredService<VerifyGitHubVulnerabilitiesConfiguration>() ??
                             throw new Exception(
                                 $"{nameof(VerifyGitHubVulnerabilitiesConfiguration)} service cannot be null");
            if (_configuration.VerifyDatabase)
            {
                _entitiesContext = serviceProvider.GetRequiredService<IEntitiesContext>() ??
                                   throw new Exception($"{nameof(IEntitiesContext)} service cannot be null for database operations");
            }
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
                Console.Error.WriteLine("Null vulnerability passed to verifier! Continuing...");
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
                return VerifyVulnerabilityInMetadata(vulnerability);
            }

            return Task.CompletedTask;
        }

        private void VerifyVulnerabilityInDatabase(PackageVulnerability vulnerability, bool withdrawn)
        {
            Console.WriteLine($"[Database] Verifying vulnerability {vulnerability.GitHubDatabaseKey}.");

            var existingVulnerability = _entitiesContext.Vulnerabilities
                .Include(v => v.AffectedRanges)
                .SingleOrDefault(v => v.GitHubDatabaseKey == vulnerability.GitHubDatabaseKey);

            if (withdrawn || !vulnerability.AffectedRanges.Any())
            {
                if (existingVulnerability != null)
                {
                    Console.Error.WriteLine(withdrawn ?
                        $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey} was withdrawn and should not be in DB!" :
                        $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey} affects no packages and should not be in DB!");
                    HasErrors = true;
                }

                return;
            }

            if (existingVulnerability == null)
            {
                Console.Error.WriteLine($"[Database] Cannot find vulnerability {vulnerability.GitHubDatabaseKey} in DB!");
                HasErrors = true;
                return;
            }

            if (existingVulnerability.Severity != vulnerability.Severity)
            {
                Console.Error.WriteLine(
                    $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey
                    }, severity does not match! GitHub: {vulnerability.Severity}, DB: {existingVulnerability.Severity}");
                HasErrors = true;
            }

            if (existingVulnerability.AdvisoryUrl != vulnerability.AdvisoryUrl)
            {
                Console.Error.WriteLine(
                    $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey
                    }, advisory URL does not match! GitHub: {vulnerability.AdvisoryUrl}, DB: { existingVulnerability.AdvisoryUrl}");
                HasErrors = true;
            }

            foreach (var range in vulnerability.AffectedRanges)
            {
                Console.WriteLine($"[Database] Verifying range affecting {range.PackageId} {range.PackageVersionRange}.");
                var existingRange = existingVulnerability.AffectedRanges
                    .SingleOrDefault(r => r.PackageId == range.PackageId && r.PackageVersionRange == range.PackageVersionRange);

                if (existingRange == null)
                {
                    Console.Error.WriteLine(
                        $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey
                        }, cannot find range {range.PackageId} {range.PackageVersionRange} in DB!");
                    HasErrors = true;
                    continue;
                }

                if (existingRange.FirstPatchedPackageVersion != range.FirstPatchedPackageVersion)
                {
                    Console.Error.WriteLine(
                        $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey
                        }, range {range.PackageId} {range.PackageVersionRange}, first patched version does not match! GitHub: {
                        range.FirstPatchedPackageVersion}, DB: {range.FirstPatchedPackageVersion}");
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
                        Console.Error.WriteLine(
                            $@"[Database] Vulnerability advisory {vulnerability.GitHubDatabaseKey
                            }, range {range.PackageId} {range.PackageVersionRange}, package {package.NormalizedVersion
                            } is not properly marked vulnerable to vulnerability!");
                        HasErrors = true;
                    }
                }
            }
        }

        private Task VerifyVulnerabilityInMetadata(PackageVulnerability gitHubAdvisory)
        {
            Console.WriteLine($"[Metadata] Verifying vulnerability {gitHubAdvisory.GitHubDatabaseKey}.");

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

            var verificationTasks = new List<Task>();
            foreach (var rangeById in rangesById)
            {
                verificationTasks.Add(VerifyVulnerabilityForRangeAsync(
                    packageId: rangeById.Key, 
                    ranges: rangeById.Value, 
                    gitHubAdvisory.AdvisoryUrl, 
                    gitHubAdvisory.GitHubDatabaseKey, 
                    gitHubAdvisory.Severity));
            }

            return Task.WhenAll(verificationTasks);
        }

        private async Task VerifyVulnerabilityForRangeAsync (
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
                        Console.Error.WriteLine(
                            $@"[Metadata] Vulnerability advisory {advisoryDatabaseKey
                                }, version {versionMetadata.Identity.Version} of package {packageId} is not marked vulnerable and is in a vulnerable range!");
                        HasErrors = true;
                    }

                    // Test whether we have any severity mismatches
                    var firstSeverityMismatch = matchingVulnerabilities
                        .FirstOrDefault(v => v.Severity != (int)advisorySeverity);
                    if (firstSeverityMismatch != null)
                    {
                        Console.Error.WriteLine(
                            $@"[Metadata] Vulnerability advisory {advisoryDatabaseKey
                                }, severities has at least one mismatch! GitHub: {advisorySeverity}, Metadata: {firstSeverityMismatch.Severity}");
                        HasErrors = true;
                    }
                }
                else
                {
                    if (hasTheVulnerability)
                    {
                        Console.Error.WriteLine(
                            $@"[Metadata] Vulnerability advisory {advisoryDatabaseKey
                                }, version {versionMetadata} of package {packageId} is marked vulnerable and is not in a vulnerable range!");
                        HasErrors = true;
                    }
                }
            }
        }

        private async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadataAsync(string package)
        {
            if (_packageMetadataResource == null)
            {
                await InitializeMetadataResourceAsync();
            }

            if (!_packageMetadata.TryGetValue(package, out IEnumerable<IPackageSearchMetadata> metadata))
            {
                metadata = await _packageMetadataResource.GetMetadataAsync(
                    package,
                    includePrerelease: true,
                    includeUnlisted: false,
                    sourceCacheContext: new SourceCacheContext(),
                    log: NuGet.Common.NullLogger.Instance,
                    token: CancellationToken.None);
                _packageMetadata[package] = metadata;
            }

            return metadata;
        }

        private async Task InitializeMetadataResourceAsync()
        {
            var providers = Repository.Provider.GetCoreV3();
            var packageSource = new PackageSource(_configuration.NuGetV3Index, "NuGet Source", isEnabled: true);
            var sourceRepository = Repository.CreateSource(providers, packageSource, FeedType.Undefined);
            _packageMetadataResource =
                await sourceRepository.GetResourceAsync<PackageMetadataResource>(CancellationToken.None);

            _packageMetadata = new Dictionary<string, IEnumerable<IPackageSearchMetadata>>();
        }
    }
}
