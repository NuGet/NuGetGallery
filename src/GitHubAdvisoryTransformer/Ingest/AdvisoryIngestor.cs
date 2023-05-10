// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using GitHubAdvisoryTransformer.Entities;
using GitHubAdvisoryTransformer.GraphQL;

namespace GitHubAdvisoryTransformer.Ingest
{
    public class AdvisoryIngestor : IAdvisoryIngestor
    {
        private readonly IGitHubVersionRangeParser _gitHubVersionRangeParser;
        private IVulnerabilityWriter _vulnerabilityWriter;

        public AdvisoryIngestor(
            IVulnerabilityWriter vulnerabilityWriter,
            IGitHubVersionRangeParser gitHubVersionRangeParser)
        {
            _vulnerabilityWriter = vulnerabilityWriter ?? throw new ArgumentNullException(nameof(vulnerabilityWriter));
            _gitHubVersionRangeParser = gitHubVersionRangeParser ?? throw new ArgumentNullException(nameof(gitHubVersionRangeParser));
        }

        public async Task IngestAsync(IReadOnlyList<SecurityAdvisory> advisories)
        {
            for (int i = 0; i < advisories.Count; i++)
            {
                Console.WriteLine($"Processing advisory {i+1} of {advisories.Count}...");
                SecurityAdvisory advisory = advisories[i];
                var vulnerabilityTuple = FromAdvisory(advisory);
                var vulnerability = vulnerabilityTuple.Item1;
                var wasWithdrawn = vulnerabilityTuple.Item2;

                _vulnerabilityWriter.WriteVulnerability(vulnerability);
            }

            _vulnerabilityWriter.FlushToFile();
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