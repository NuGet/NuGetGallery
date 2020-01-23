// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageDeprecationServiceFacts
    {
        public class TheUpdateDeprecationMethod : TestContainer
        {
            public static IEnumerable<object[]> ThrowsIfPackagesEmpty_Data => MemberDataHelper.AsDataSet(null, new Package[0]);

            [Theory]
            [MemberData(nameof(ThrowsIfPackagesEmpty_Data))]
            public async Task ThrowsIfPackagesEmpty(IReadOnlyList<Package> packages)
            {
                var service = Get<PackageDeprecationService>();

                var user = new User { Key = 1 };
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.UpdateDeprecation(
                        packages,
                        PackageDeprecationStatus.NotDeprecated,
                        alternatePackageRegistration: null,
                        alternatePackage: null,
                        customMessage: null,
                        user: user));
            }

            [Fact]
            public async Task ThrowsIfPackagesHaveDifferentRegistrations()
            {
                var service = Get<PackageDeprecationService>();

                var packages = new[]
                {
                    new Package { PackageRegistrationKey = 1 },
                    new Package { PackageRegistrationKey = 2 },
                };

                var user = new User { Key = 1 };
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.UpdateDeprecation(
                        packages,
                        PackageDeprecationStatus.NotDeprecated,
                        alternatePackageRegistration: null,
                        alternatePackage: null,
                        customMessage: null,
                        user: user));
            }

            [Fact]
            public async Task DeletesExistingDeprecationsIfStatusNotDeprecated()
            {
                // Arrange
                var id = "theId";
                var registration = new PackageRegistration { Id = id };
                var packageWithDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "1.0.0",
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "2.0.0"
                };

                var packageWithDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "3.0.0",
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() }
                };

                var packageWithoutDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "4.0.0"
                };

                var packages = new[]
                {
                    packageWithDeprecation1,
                    packageWithoutDeprecation1,
                    packageWithDeprecation2,
                    packageWithoutDeprecation2
                };

                var transactionMock = new Mock<IDbContextTransaction>();
                transactionMock
                    .Setup(x => x.Commit())
                    .Verifiable();

                var databaseMock = new Mock<IDatabase>();
                databaseMock
                    .Setup(x => x.BeginTransaction())
                    .Returns(transactionMock.Object);

                var context = GetFakeContext();
                context.SetupDatabase(databaseMock.Object);
                context.Deprecations.AddRange(
                    packages
                        .Select(p => p.Deprecations.SingleOrDefault())
                        .Where(d => d != null));

                var packageUpdateService = GetMock<IPackageUpdateService>();
                packageUpdateService
                    .Setup(b => b.UpdatePackagesAsync(packages, true))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var auditingService = GetService<IAuditingService>();

                var telemetryService = GetMock<ITelemetryService>();
                telemetryService
                    .Setup(x => x.TrackPackageDeprecate(packages, PackageDeprecationStatus.NotDeprecated, null, null, false))
                    .Verifiable();

                var user = new User { Key = 1 };
                var service = Get<PackageDeprecationService>();

                // Act
                await service.UpdateDeprecation(
                    packages,
                    PackageDeprecationStatus.NotDeprecated,
                    alternatePackageRegistration: null,
                    alternatePackage: null,
                    customMessage: null,
                    user: user);

                // Assert
                context.VerifyCommitChanges();
                Assert.Equal(0, context.Deprecations.Count());
                packageUpdateService.Verify();
                transactionMock.Verify();
                telemetryService.Verify();

                foreach (var package in packages)
                {
                    Assert.Empty(package.Deprecations);

                    auditingService.WroteRecord<PackageAuditRecord>(
                        r => r.Action == AuditedPackageAction.Undeprecate
                        && r.Reason == PackageUndeprecatedVia.Web
                        && r.DeprecationRecord == null
                        && r.Id == id
                        && r.PackageRecord.NormalizedVersion == package.NormalizedVersion);
                }
            }

            [Fact]
            public async Task ReplacesExistingDeprecations()
            {
                // Arrange
                var lastTimestamp = new DateTime(2019, 3, 4);

                var id = "theId";
                var registration = new PackageRegistration { Id = id };
                var packageWithDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "1.0.0",
                    Deprecations = new List<PackageDeprecation> { new PackageDeprecation() },
                    LastEdited = lastTimestamp
                };

                var packageWithoutDeprecation1 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "2.0.0",
                    LastEdited = lastTimestamp
                };

                var packageWithDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "3.0.0",
                    LastEdited = lastTimestamp,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                        }
                    }
                };

                var packageWithoutDeprecation2 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "4.0.0",
                    LastEdited = lastTimestamp
                };

                var packageWithDeprecation3 = new Package
                {
                    PackageRegistration = registration,
                    NormalizedVersion = "5.0.0",
                    LastEdited = lastTimestamp,
                    Deprecations = new List<PackageDeprecation>
                    {
                        new PackageDeprecation
                        {
                        }
                    }
                };

                var packages = new[]
                {
                    packageWithDeprecation1,
                    packageWithoutDeprecation1,
                    packageWithDeprecation2,
                    packageWithoutDeprecation2,
                    packageWithDeprecation3
                };

                var transactionMock = new Mock<IDbContextTransaction>();
                transactionMock
                    .Setup(x => x.Commit())
                    .Verifiable();

                var databaseMock = new Mock<IDatabase>();
                databaseMock
                    .Setup(x => x.BeginTransaction())
                    .Returns(transactionMock.Object);

                var context = GetFakeContext();
                context.SetupDatabase(databaseMock.Object);
                context.Deprecations.AddRange(
                    packages
                        .Select(p => p.Deprecations.SingleOrDefault())
                        .Where(d => d != null));

                var packageUpdateService = GetMock<IPackageUpdateService>();
                packageUpdateService
                    .Setup(b => b.UpdatePackagesAsync(packages, true))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var auditingService = GetService<IAuditingService>();

                var status = (PackageDeprecationStatus)99;

                var alternatePackageRegistration = new PackageRegistration();
                var alternatePackage = new Package();

                var telemetryService = GetMock<ITelemetryService>();
                telemetryService
                    .Setup(x => x.TrackPackageDeprecate(packages, status, alternatePackageRegistration, alternatePackage, true))
                    .Verifiable();

                var service = Get<PackageDeprecationService>();

                var customMessage = "message";
                var user = new User { Key = 1 };

                // Act
                await service.UpdateDeprecation(
                    packages,
                    status,
                    alternatePackageRegistration,
                    alternatePackage,
                    customMessage,
                    user);

                // Assert
                context.VerifyCommitChanges();
                databaseMock.Verify();
                transactionMock.Verify();
                packageUpdateService.Verify();
                telemetryService.Verify();

                Assert.Equal(packages.Count(), context.Deprecations.Count());
                foreach (var package in packages)
                {
                    var deprecation = package.Deprecations.Single();
                    Assert.Contains(deprecation, context.Deprecations);
                    Assert.Equal(status, deprecation.Status);
                    Assert.Equal(alternatePackageRegistration, deprecation.AlternatePackageRegistration);
                    Assert.Equal(alternatePackage, deprecation.AlternatePackage);
                    Assert.Equal(customMessage, deprecation.CustomMessage);

                    auditingService.WroteRecord<PackageAuditRecord>(
                        r => r.Action == (status == PackageDeprecationStatus.NotDeprecated ? AuditedPackageAction.Undeprecate : AuditedPackageAction.Deprecate)
                        && r.Reason == (status == PackageDeprecationStatus.NotDeprecated ? PackageUndeprecatedVia.Web : PackageDeprecatedVia.Web)
                        && r.DeprecationRecord == null
                        && r.Id == id
                        && r.PackageRecord.NormalizedVersion == package.NormalizedVersion);
                }
            }
        }

        public class TheGetDeprecationByPackageMethod : TestContainer
        {
            [Fact]
            public void GetsDeprecationOfPackage()
            {
                // Arrange
                var differentDeprecation = new PackageDeprecation
                {
                    Package = new Package
                    {
                        PackageRegistration = new PackageRegistration {  Id = "Bar" }
                    }
                };

                var matchingDeprecation1 = new PackageDeprecation
                {
                    Package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Foo" }
                    }
                };

                var matchingDeprecation2 = new PackageDeprecation
                {
                    Package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "Foo" }
                    }
                };

                var context = GetFakeContext();
                context.Deprecations.AddRange(
                    new[] { differentDeprecation, matchingDeprecation1, matchingDeprecation2 });

                var target = Get<PackageDeprecationService>();

                // Act
                var result = target.GetDeprecationsById("Foo");

                // Assert
                Assert.Equal(2, result.Count);
                Assert.Equal(matchingDeprecation1, result[0]);
                Assert.Equal(matchingDeprecation2, result[1]);
            }
        }
    }
}
