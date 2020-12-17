﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageVulnerabilitiesManagementServiceFacts
    {
        public class TheApplyExistingVulnerabilitiesToPackageMethod : MethodFacts
        {
            [Fact]
            public void IfNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() => Service.ApplyExistingVulnerabilitiesToPackage(null));
            }

            [Fact]
            public void AppliesCorrectVulnerabilities()
            {
                // Arrange
                var registration = new PackageRegistration
                {
                    Id = "id"
                };

                var package = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "1.0.0"
                };

                var wrongIdRange = new VulnerablePackageVersionRange
                {
                    PackageId = "wrongId",
                    PackageVersionRange = "(,)"
                };

                var satisfiedRange = new VulnerablePackageVersionRange
                {
                    PackageId = registration.Id,
                    PackageVersionRange = "[1.0.0, )"
                };

                var unsatisfiedRange = new VulnerablePackageVersionRange
                {
                    PackageId = registration.Id,
                    PackageVersionRange = "(, 1.0.0)"
                };

                Context.VulnerableRanges.AddRange(
                    new[] { wrongIdRange, satisfiedRange, unsatisfiedRange });

                // Act
                Service.ApplyExistingVulnerabilitiesToPackage(package);

                // Assert
                Assert.Single(package.VulnerablePackageRanges, satisfiedRange);
                Assert.Single(satisfiedRange.Packages, package);
            }
        }

        public class TheUpdateVulnerabilityMethod : MethodFacts
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task IfNull_ThrowsArgumentNullException(bool withdrawn)
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => Service.UpdateVulnerabilityAsync(null, withdrawn));
            }

            [Fact]
            public async Task WithNoExistingVulnerability_Withdrawn_DoesNotAdd()
            {
                // Arrange
                var id = "theId";
                var versionRange = new VersionRange(NuGetVersion.Parse("1.0.0")).ToNormalizedString();
                var vulnerability = new PackageVulnerability();
                var range = new VulnerablePackageVersionRange
                {
                    Vulnerability = vulnerability,
                    PackageId = id,
                    PackageVersionRange = versionRange
                };

                vulnerability.AffectedRanges.Add(range);

                // Act
                await Service.UpdateVulnerabilityAsync(vulnerability, true);

                // Assert
                Assert.False(Context.Vulnerabilities.AnySafe());
                Assert.False(Context.VulnerableRanges.AnySafe());
                UpdateServiceMock.Verify(
                    x => x.UpdatePackagesAsync(It.IsAny<IReadOnlyList<Package>>(), It.IsAny<bool>()),
                    Times.Never);

                VerifyTransaction();
            }

            [Fact]
            public async Task WithNoExistingVulnerability_NoRanges_DoesNotAdd()
            {
                // Arrange
                var vulnerability = new PackageVulnerability();

                // Act
                await Service.UpdateVulnerabilityAsync(vulnerability, true);

                // Assert
                Assert.False(Context.Vulnerabilities.AnySafe());
                Assert.False(Context.VulnerableRanges.AnySafe());
                UpdateServiceMock.Verify(
                    x => x.UpdatePackagesAsync(It.IsAny<IReadOnlyList<Package>>(), It.IsAny<bool>()),
                    Times.Never);

                VerifyTransaction();
            }

            [Fact]
            public async Task WithNoExistingVulnerability_WithRanges_Adds()
            {
                // Arrange
                var id = "theId";
                var versionRange = new VersionRange(NuGetVersion.Parse("1.0.0")).ToNormalizedString();
                var vulnerability = new PackageVulnerability();
                var range = new VulnerablePackageVersionRange
                {
                    Vulnerability = vulnerability,
                    PackageId = id,
                    PackageVersionRange = versionRange
                };

                vulnerability.AffectedRanges.Add(range);

                var registration = new PackageRegistration { Id = id };
                Context.PackageRegistrations.Add(registration);
                var vulnerablePackage = new Package { PackageRegistration = registration, NormalizedVersion = "1.0.1" };
                registration.Packages.Add(vulnerablePackage);

                var notVulnerablePackage = new Package { PackageRegistration = registration, NormalizedVersion = "0.0.0" };
                registration.Packages.Add(notVulnerablePackage);

                UpdateServiceMock
                    .Setup(x => x.UpdatePackagesAsync(new[] { vulnerablePackage }, true))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                await Service.UpdateVulnerabilityAsync(vulnerability, false);

                // Assert
                Assert.Single(Context.Vulnerabilities, vulnerability);
                Assert.Single(Context.VulnerableRanges, range);

                UpdateServiceMock.Verify();
                VerifyTransaction();
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WithExistingVulnerability_Withdrawn_RemovesAndUnmarks(bool hasExistingVulnerablePackages)
            {
                // Arrange
                var key = 1;
                var vulnerability = new PackageVulnerability { GitHubDatabaseKey = key };
                var existingVulnerability = new PackageVulnerability
                {
                    GitHubDatabaseKey = key,
                    Severity = PackageVulnerabilitySeverity.Moderate
                };

                Context.Vulnerabilities.Add(existingVulnerability);
                var id = "theId";
                var versionRange = new VersionRange(NuGetVersion.Parse("1.0.0")).ToNormalizedString();
                var range = new VulnerablePackageVersionRange
                {
                    Vulnerability = vulnerability,
                    PackageId = id,
                    PackageVersionRange = versionRange
                };

                vulnerability.AffectedRanges.Add(range);

                if (hasExistingVulnerablePackages)
                {
                    var existingVulnerablePackage = new Package();
                    var existingRange = new VulnerablePackageVersionRange
                    {
                        Vulnerability = existingVulnerability
                    };

                    Context.VulnerableRanges.Add(existingRange);
                    existingVulnerability.AffectedRanges.Add(existingRange);
                    existingRange.Packages.Add(existingVulnerablePackage);
                    vulnerability.AffectedRanges.Add(existingRange);

                    UpdateServiceMock
                        .Setup(x => x.UpdatePackagesAsync(new[] { existingVulnerablePackage }, true))
                        .Returns(Task.CompletedTask)
                        .Verifiable();
                }

                var service = GetService<PackageVulnerabilitiesManagementService>();

                // Act
                await service.UpdateVulnerabilityAsync(vulnerability, true);

                // Assert
                Assert.False(Context.Vulnerabilities.AnySafe());
                Assert.DoesNotContain(range, Context.VulnerableRanges);
                UpdateServiceMock.Verify();

                VerifyTransaction();
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WithExistingVulnerability_NotWithdrawn_NoRanges_RemovesAndUnmarks(bool hasExistingVulnerablePackages)
            {
                // Arrange
                var key = 1;
                var vulnerability = new PackageVulnerability { GitHubDatabaseKey = key };
                var existingVulnerability = new PackageVulnerability
                {
                    GitHubDatabaseKey = key,
                    Severity = PackageVulnerabilitySeverity.Moderate
                };

                Context.Vulnerabilities.Add(existingVulnerability);

                if (hasExistingVulnerablePackages)
                {
                    var existingVulnerablePackage = new Package();
                    var existingRange = new VulnerablePackageVersionRange
                    {
                        Vulnerability = existingVulnerability
                    };

                    Context.VulnerableRanges.Add(existingRange);
                    existingVulnerability.AffectedRanges.Add(existingRange);
                    existingRange.Packages.Add(existingVulnerablePackage);

                    UpdateServiceMock
                        .Setup(x => x.UpdatePackagesAsync(new[] { existingVulnerablePackage }, true))
                        .Returns(Task.CompletedTask)
                        .Verifiable();
                }

                var service = GetService<PackageVulnerabilitiesManagementService>();

                // Act
                await service.UpdateVulnerabilityAsync(vulnerability, false);

                // Assert
                Assert.False(Context.Vulnerabilities.AnySafe());
                Assert.False(Context.VulnerableRanges.AnySafe());
                UpdateServiceMock.Verify();

                VerifyTransaction();
            }

            public static IEnumerable<object[]> WithExisting_NotWithdrawn_UpdatesPackages_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.BooleanDataSet(),
                    MemberDataHelper.BooleanDataSet());

            [Theory]
            [MemberData(nameof(WithExisting_NotWithdrawn_UpdatesPackages_Data))]
            public async Task WithExistingVulnerability_NotWithdrawn_UpdatesPackages(
                bool hasExistingVulnerablePackages,
                bool wasUpdated)
            {
                // Arrange
                var key = 1;
                var severity = PackageVulnerabilitySeverity.Low;
                var newSeverity = PackageVulnerabilitySeverity.Critical;
                var url = "http://hi";
                var newUrl = "https://howdy";
                var vulnerability = new PackageVulnerability
                {
                    GitHubDatabaseKey = key,
                    Severity = wasUpdated ? newSeverity : severity,
                    AdvisoryUrl = wasUpdated ? newUrl : url
                };

                var existingVulnerability = new PackageVulnerability
                {
                    GitHubDatabaseKey = key,
                    Severity = severity,
                    AdvisoryUrl = url
                };

                Context.Vulnerabilities.Add(existingVulnerability);

                var id = "theId";
                var versionRange = new VersionRange(NuGetVersion.Parse("1.0.0")).ToNormalizedString();
                var range = new VulnerablePackageVersionRange
                {
                    Vulnerability = vulnerability,
                    PackageId = id,
                    PackageVersionRange = versionRange
                };

                vulnerability.AffectedRanges.Add(range);

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var expectedPackagesToUpdate = new List<Package>();
                var expectedVulnerablePackages = new List<Package>();
                if (hasExistingVulnerablePackages)
                {
                    // Any packages that are already vulnerable to this vulnerability but not associated with this package vulnerability should be updated if the vulnerability is updated.
                    var existingVulnerablePackage = new Package
                    {
                        PackageRegistration = registration,
                        NormalizedVersion = "0.0.5"
                    };

                    var existingVulnerablePackageVersion = NuGetVersion.Parse(existingVulnerablePackage.NormalizedVersion);
                    var existingRange = new VulnerablePackageVersionRange
                    {
                        Vulnerability = existingVulnerability,
                        PackageId = id,
                        PackageVersionRange = new VersionRange(existingVulnerablePackageVersion, true, existingVulnerablePackageVersion, true).ToNormalizedString()
                    };

                    Context.VulnerableRanges.Add(existingRange);
                    existingRange.Packages.Add(existingVulnerablePackage);
                    existingVulnerability.AffectedRanges.Add(existingRange);
                    vulnerability.AffectedRanges.Add(existingRange);

                    expectedVulnerablePackages.Add(existingVulnerablePackage);
                    if (wasUpdated)
                    {
                        expectedPackagesToUpdate.Add(existingVulnerablePackage);
                    }

                    // If an existing vulnerable range is updated, its packages should be updated.
                    var existingVulnerablePackageWithUpdatedRange = new Package
                    {
                        PackageRegistration = registration,
                        NormalizedVersion = "0.0.9"
                    };

                    var existingVulnerablePackageVersionWithUpdatedRange = NuGetVersion.Parse(existingVulnerablePackageWithUpdatedRange.NormalizedVersion);
                    var existingRangeWithUpdatedRange = new VulnerablePackageVersionRange
                    {
                        Vulnerability = existingVulnerability,
                        PackageId = id,
                        PackageVersionRange = new VersionRange(existingVulnerablePackageVersionWithUpdatedRange, true, existingVulnerablePackageVersionWithUpdatedRange, true).ToNormalizedString()
                    };

                    Context.VulnerableRanges.Add(existingRangeWithUpdatedRange);
                    existingRangeWithUpdatedRange.Packages.Add(existingVulnerablePackageWithUpdatedRange);
                    existingVulnerability.AffectedRanges.Add(existingRangeWithUpdatedRange);

                    var updatedExistingRange = new VulnerablePackageVersionRange
                    {
                        Vulnerability = existingRangeWithUpdatedRange.Vulnerability,
                        PackageId = existingRangeWithUpdatedRange.PackageId,
                        PackageVersionRange = existingRangeWithUpdatedRange.PackageVersionRange,
                        FirstPatchedPackageVersion = "1.0.0"
                    };

                    vulnerability.AffectedRanges.Add(updatedExistingRange);

                    expectedVulnerablePackages.Add(existingVulnerablePackageWithUpdatedRange);
                    expectedPackagesToUpdate.Add(existingVulnerablePackageWithUpdatedRange);

                    // If a package vulnerability is missing from the new vulnerability, it should be removed.
                    var existingMissingVulnerablePackage = new Package
                    {
                        PackageRegistration = registration,
                        NormalizedVersion = "0.0.6"
                    };

                    var existingMissingVulnerablePackageVersion = NuGetVersion.Parse(existingMissingVulnerablePackage.NormalizedVersion);
                    var existingMissingRange = new VulnerablePackageVersionRange
                    {
                        Vulnerability = existingVulnerability,
                        PackageId = id,
                        PackageVersionRange = new VersionRange(existingMissingVulnerablePackageVersion, true, existingMissingVulnerablePackageVersion, true).ToNormalizedString()
                    };

                    Context.VulnerableRanges.Add(existingMissingRange);
                    existingMissingRange.Packages.Add(existingMissingVulnerablePackage);
                    existingVulnerability.AffectedRanges.Add(existingMissingRange);

                    expectedPackagesToUpdate.Add(existingMissingVulnerablePackage);
                }

                Context.PackageRegistrations.Add(registration);

                // A package that fits in the version range but is not vulnerable yet should be vulnerable and updated.
                var newlyVulnerablePackage = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "1.0.2"
                };

                registration.Packages.Add(newlyVulnerablePackage);
                expectedVulnerablePackages.Add(newlyVulnerablePackage);
                expectedPackagesToUpdate.Add(newlyVulnerablePackage);

                // A package that is not vulnerable and does not fit in the version range should not be touched.
                var neverVulnerablePackage = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "0.0.1"
                };

                registration.Packages.Add(neverVulnerablePackage);

                if (expectedPackagesToUpdate.Any())
                {
                    UpdateServiceMock
                        .Setup(x => x.UpdatePackagesAsync(
                            It.Is<IReadOnlyList<Package>>(
                                p => p
                                    .OrderBy(v => v.NormalizedVersion)
                                    .SequenceEqual(
                                        expectedPackagesToUpdate.OrderBy(v => v.NormalizedVersion))),
                            true))
                        .Returns(Task.CompletedTask)
                        .Verifiable();
                }

                var service = GetService<PackageVulnerabilitiesManagementService>();

                // Act
                await service.UpdateVulnerabilityAsync(vulnerability, false);

                // Assert
                Assert.Contains(existingVulnerability, Context.Vulnerabilities);
                Assert.Contains(range, Context.VulnerableRanges);
                Assert.Equal(existingVulnerability, range.Vulnerability);
                Assert.Equal(wasUpdated ? newSeverity : severity, existingVulnerability.Severity);
                Assert.Equal(wasUpdated ? newUrl : url, existingVulnerability.AdvisoryUrl);
                Assert.Equal(
                    expectedVulnerablePackages.OrderBy(p => p.NormalizedVersion),
                    Context.VulnerableRanges.SelectMany(pv => pv.Packages).OrderBy(p => p.NormalizedVersion));

                UpdateServiceMock.Verify();

                VerifyTransaction();
            }
        }

        public class MethodFacts : TestContainer
        {
            public MethodFacts()
            {
                _transactionMock = new Mock<IDbContextTransaction>();
                _databaseMock = new Mock<IDatabase>();
                Context = GetFakeContext();
                UpdateServiceMock = GetMock<IPackageUpdateService>();
                Service = GetService<PackageVulnerabilitiesManagementService>();

                _transactionMock
                    .Setup(x => x.Commit())
                    .Verifiable();

                _databaseMock
                    .Setup(x => x.BeginTransaction())
                    .Returns(_transactionMock.Object)
                    .Verifiable();

                Context.SetupDatabase(_databaseMock.Object);
            }

            private Mock<IDbContextTransaction> _transactionMock { get; }
            private Mock<IDatabase> _databaseMock { get; }
            protected FakeEntitiesContext Context { get; }
            protected Mock<IPackageUpdateService> UpdateServiceMock { get; }
            protected PackageVulnerabilitiesManagementService Service { get; }

            protected void VerifyTransaction()
            {
                _transactionMock.Verify();
                _databaseMock.Verify();
            }
        }
    }
}