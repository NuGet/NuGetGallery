// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.GitHub.GraphQL;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.GitHub.Ingest
{
    public class AdvisoryIngestor : IAdvisoryIngestor
    {
        private readonly IGitHubVersionRangeParser _gitHubVersionRangeParser;
        private readonly IVulnerabilityWriter _vulnerabilityWriter;
        private readonly ILogger<AdvisoryIngestor> _logger;

        public AdvisoryIngestor(
            IGitHubVersionRangeParser gitHubVersionRangeParser,
            IVulnerabilityWriter vulnerabilityWriter,
            ILogger<AdvisoryIngestor> logger)
        {
            _gitHubVersionRangeParser = gitHubVersionRangeParser ?? throw new ArgumentNullException(nameof(gitHubVersionRangeParser));
            _vulnerabilityWriter = vulnerabilityWriter ?? throw new ArgumentNullException(nameof(vulnerabilityWriter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task IngestAsync(IReadOnlyList<SecurityAdvisory> advisories)
        {
            for (int i = 0; i < advisories.Count; i++)
            {
                _logger.LogInformation("Processing advisory {Current} of {Total}...", i + 1, advisories.Count);
                SecurityAdvisory advisory = advisories[i];
                var vulnerabilityTuple = FromAdvisory(advisory);
                var vulnerability = vulnerabilityTuple.Item1;
                var wasWithdrawn = vulnerabilityTuple.Item2;
                await _vulnerabilityWriter.WriteVulnerabilityAsync(vulnerability, wasWithdrawn);
            }

            await _vulnerabilityWriter.FlushAsync();
        }

        private Tuple<PackageVulnerability, bool> FromAdvisory(SecurityAdvisory advisory)
        {
            var vulnerability = new PackageVulnerability
            {
                GitHubDatabaseKey = advisory.DatabaseId,
                Severity = (PackageVulnerabilitySeverity)Enum.Parse(typeof(PackageVulnerabilitySeverity), advisory.Severity, ignoreCase: true),
                AdvisoryUrl = advisory.Permalink
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