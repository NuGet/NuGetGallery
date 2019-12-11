// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GitHubVulnerabilities2Db.Ingest
{
    public class AdvisoryIngestor : IAdvisoryIngestor
    {
        private readonly IPackageVulnerabilityService _packageVulnerabilityService;
        private readonly IGitHubVersionRangeParser _gitHubVersionRangeParser;

        public AdvisoryIngestor(
            IPackageVulnerabilityService packageVulnerabilityService,
            IGitHubVersionRangeParser gitHubVersionRangeParser)
        {
            _packageVulnerabilityService = packageVulnerabilityService ?? throw new ArgumentNullException(nameof(packageVulnerabilityService));
            _gitHubVersionRangeParser = gitHubVersionRangeParser ?? throw new ArgumentNullException(nameof(gitHubVersionRangeParser));
        }

        public async Task IngestAsync(IReadOnlyList<SecurityAdvisory> advisories)
        {
            foreach (var advisory in advisories)
            {
                var vulnerabilityTuple = FromAdvisory(advisory);
                var vulnerability = vulnerabilityTuple.Item1;
                var wasWithdrawn = vulnerabilityTuple.Item2;
                await _packageVulnerabilityService.UpdateVulnerabilityAsync(vulnerability, wasWithdrawn);
            }
        }

        private Tuple<PackageVulnerability, bool> FromAdvisory(SecurityAdvisory advisory)
        {
            var vulnerability = new PackageVulnerability
            {
                GitHubDatabaseKey = advisory.DatabaseId,
                Severity = (PackageVulnerabilitySeverity)Enum.Parse(typeof(PackageVulnerabilitySeverity), advisory.Severity, ignoreCase: true),
                AdvisoryUrl = advisory.GetPermalink()
            };

            foreach (var securityVulnerability in advisory.Vulnerabilities?.Edges?.Select(e => e.Node) ?? Enumerable.Empty<SecurityVulnerability>())
            {
                var packageVulnerability = FromVulnerability(vulnerability, securityVulnerability);
                vulnerability.AffectedRanges.Add(packageVulnerability);
            }

            return Tuple.Create(vulnerability, advisory.WithdrawnAt != null);
        }

        private VulnerablePackageVersionRange FromVulnerability(PackageVulnerability vulnerability, SecurityVulnerability securityVulnerability)
        {
            return new VulnerablePackageVersionRange
            {
                Vulnerability = vulnerability,
                PackageId = securityVulnerability.Package.Name,
                PackageVersionRange = _gitHubVersionRangeParser.ToNuGetVersionRange(securityVulnerability.VulnerableVersionRange).ToNormalizedString(),
                FirstPatchedPackageVersion = securityVulnerability.FirstPatchedVersion?.Identifier
            };
        }
    }
}