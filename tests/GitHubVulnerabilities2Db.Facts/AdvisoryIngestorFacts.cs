// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.GraphQL;
using GitHubVulnerabilities2Db.Ingest;
using Moq;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;

namespace GitHubVulnerabilities2Db.Facts
{
    public class AdvisoryIngestorFacts
    {
        public class TheIngestMethodFacts : MethodFacts
        {
            [Fact]
            public async Task IngestsNone()
            {
                // Act
                await Ingestor.IngestAsync(Enumerable.Empty<SecurityAdvisory>().ToList());

                // Assert
                PackageVulnerabilityServiceMock
                    .Verify(
                        x => x.UpdateVulnerabilityAsync(It.IsAny<PackageVulnerability>(), It.IsAny<bool>()),
                        Times.Never);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task IngestsAdvisoryWithoutVulnerability(bool withdrawn)
            {
                // Arrange
                var advisory = new SecurityAdvisory
                {
                    DatabaseId = 1,
                    GhsaId = "ghsa",
                    Severity = "MODERATE",
                    WithdrawnAt = withdrawn ? new DateTimeOffset() : (DateTimeOffset?)null
                };

                PackageVulnerabilityServiceMock
                    .Setup(x => x.UpdateVulnerabilityAsync(It.IsAny<PackageVulnerability>(), withdrawn))
                    .Callback<PackageVulnerability, bool>((vulnerability, wasWithdrawn) =>
                    {
                        Assert.Equal(advisory.DatabaseId, vulnerability.GitHubDatabaseKey);
                        Assert.Equal(PackageVulnerabilitySeverity.Moderate, vulnerability.Severity);
                        Assert.Equal(advisory.GetPermalink(), vulnerability.AdvisoryUrl);
                    })
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                await Ingestor.IngestAsync(new[] { advisory });

                // Assert
                PackageVulnerabilityServiceMock.Verify();
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(true, false)]
            [InlineData(false, true)]
            [InlineData(true, true)]
            public async Task IngestsAdvisory(bool withdrawn, bool vulnerabilityHasFirstPatchedVersion)
            {
                // Arrange
                var securityVulnerability = new SecurityVulnerability
                {
                    Package = new SecurityVulnerabilityPackage { Name = "crested.gecko" },
                    VulnerableVersionRange = "homeOnTheRange",
                    FirstPatchedVersion = vulnerabilityHasFirstPatchedVersion 
                        ? new SecurityVulnerabilityPackageVersion { Identifier = "1.2.3" } : null
                };

                var advisory = new SecurityAdvisory
                {
                    DatabaseId = 1,
                    GhsaId = "ghsa",
                    Severity = "CRITICAL",
                    WithdrawnAt = withdrawn ? new DateTimeOffset() : (DateTimeOffset?)null,
                    Vulnerabilities = new ConnectionResponseData<SecurityVulnerability>
                    {
                        Edges = new[]
                        {
                            new Edge<SecurityVulnerability>
                            {
                                Node = securityVulnerability
                            }
                        }
                    }
                };

                securityVulnerability.Advisory = advisory;

                var versionRange = VersionRange.Parse("[1.0.0, 1.0.0]");
                GitHubVersionRangeParserMock
                    .Setup(x => x.ToNuGetVersionRange(securityVulnerability.VulnerableVersionRange))
                    .Returns(versionRange);

                PackageVulnerabilityServiceMock
                    .Setup(x => x.UpdateVulnerabilityAsync(It.IsAny<PackageVulnerability>(), withdrawn))
                    .Callback<PackageVulnerability, bool>((vulnerability, wasWithdrawn) =>
                    {
                        Assert.Equal(advisory.DatabaseId, vulnerability.GitHubDatabaseKey);
                        Assert.Equal(PackageVulnerabilitySeverity.Critical, vulnerability.Severity);
                        Assert.Equal(advisory.GetPermalink(), vulnerability.AdvisoryUrl);

                        var range = vulnerability.AffectedRanges.Single();
                        Assert.Equal(securityVulnerability.Package.Name, range.PackageId);
                        Assert.Equal(versionRange.ToNormalizedString(), range.PackageVersionRange);
                        Assert.Equal(securityVulnerability.FirstPatchedVersion?.Identifier, range.FirstPatchedPackageVersion);
                    })
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                await Ingestor.IngestAsync(new[] { advisory });

                // Assert
                PackageVulnerabilityServiceMock.Verify();
            }
        }

        public class MethodFacts
        {
            public MethodFacts()
            {
                PackageVulnerabilityServiceMock = new Mock<IPackageVulnerabilityService>();
                GitHubVersionRangeParserMock = new Mock<IGitHubVersionRangeParser>();
                Ingestor = new AdvisoryIngestor(
                    PackageVulnerabilityServiceMock.Object,
                    GitHubVersionRangeParserMock.Object);
            }

            public Mock<IPackageVulnerabilityService> PackageVulnerabilityServiceMock { get; }
            public Mock<IGitHubVersionRangeParser> GitHubVersionRangeParserMock { get; }
            public AdvisoryIngestor Ingestor { get; }
        }
    }
}