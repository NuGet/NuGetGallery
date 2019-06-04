// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageUpdateServiceFacts
    {
        public class TheMarkPackageListedMethod : TestContainer
        {
            [Fact]
            public async Task ThrowsWhenPackageDeleted()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package
                {
                    Version = "1.0",
                    PackageRegistration = packageRegistration,
                    Listed = false,
                    PackageStatusKey = PackageStatus.Deleted,
                };

                var service = Get<PackageUpdateService>();

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.MarkPackageListedAsync(package));
            }

            [Fact]
            public async Task ThrowsWhenPackageFailedValidation()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package
                {
                    Version = "1.0",
                    PackageRegistration = packageRegistration,
                    Listed = false,
                    PackageStatusKey = PackageStatus.FailedValidation,
                };

                var service = Get<PackageUpdateService>();

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.MarkPackageListedAsync(package));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var auditingService = GetMock<IAuditingService>();

                // Act
                var service = Get<PackageUpdateService>();
                await service.MarkPackageListedAsync(package);

                // Assert
                auditingService
                    .Verify(
                        x => x.SaveAuditRecordAsync(It.Is<PackageAuditRecord>(ar =>
                            ar.Action == AuditedPackageAction.List
                            && ar.Id == package.PackageRegistration.Id
                            && ar.Version == package.Version)),
                        Times.Once());
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var telemetryService = GetMock<ITelemetryService>();

                var service = Get<PackageUpdateService>();
                await service.MarkPackageListedAsync(package);

                telemetryService.Verify(
                    x => x.TrackPackageListed(package),
                    Times.Once);
            }
        }

        public class TheMarkPackageUnlistedMethod : TestContainer
        {
            public enum PackageLatestState
            {
                Not,
                Latest,
                LatestStable,
                LatestSemVer2,
                LatestStableSemVer2
            }

            public static IEnumerable<object[]> OnLatestPackageVersionSetsPreviousToLatestVersion_Data =>
                MemberDataHelper.EnumDataSet<PackageLatestState>();

            [Theory]
            [MemberData(nameof(OnLatestPackageVersionSetsPreviousToLatestVersion_Data))]
            public async Task OnLatestPackageVersionSetsPreviousToLatestVersion(PackageLatestState packageLatestState)
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };

                var firstPackage = new Package
                {
                    Version = "1.0.1",
                    PackageRegistration = packageRegistration
                };

                switch (packageLatestState)
                {
                    case PackageLatestState.Latest:
                        firstPackage.IsLatest = true;
                        break;
                    case PackageLatestState.LatestStable:
                        firstPackage.IsLatestStable = true;
                        break;
                    case PackageLatestState.LatestSemVer2:
                        firstPackage.IsLatestSemVer2 = true;
                        break;
                    case PackageLatestState.LatestStableSemVer2:
                        firstPackage.IsLatestStableSemVer2 = true;
                        break;
                }

                var secondPackage = new Package
                {
                    Version = "1.0.0",
                    PackageRegistration = packageRegistration
                };

                var packages = new[] { firstPackage, secondPackage }.ToList();
                packageRegistration.Packages = packages;

                var packageService = GetMock<IPackageService>();
                if (packageLatestState != PackageLatestState.Not)
                {
                    packageService
                        .Setup(x => x.UpdateIsLatestAsync(packageRegistration, false))
                        .Returns(Task.CompletedTask)
                        .Verifiable();
                }

                var service = Get<PackageUpdateService>();
                await service.MarkPackageUnlistedAsync(firstPackage);

                packageService.Verify();
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true };
                var auditingService = GetMock<IAuditingService>();

                // Act
                var service = Get<PackageUpdateService>();
                await service.MarkPackageUnlistedAsync(package);

                // Assert
                auditingService
                    .Verify(
                        x => x.SaveAuditRecordAsync(It.Is<PackageAuditRecord>(ar =>
                            ar.Action == AuditedPackageAction.Unlist
                            && ar.Id == package.PackageRegistration.Id
                            && ar.Version == package.Version)),
                        Times.Once());
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true };
                var telemetryService = GetMock<ITelemetryService>();

                var service = Get<PackageUpdateService>();
                await service.MarkPackageUnlistedAsync(package);

                telemetryService.Verify(
                    x => x.TrackPackageUnlisted(package),
                    Times.Once);
            }
        }

        public class TheUpdatePackagesAsyncMethod : TestContainer
        {
            private readonly Mock<IEntitiesContext> _mockEntitiesContext;
            private readonly Mock<IPackageService> _mockPackageService;
            private readonly Mock<IDatabase> _mockDatabase;
            private readonly Mock<IDbContextTransaction> _mockTransaction;

            public TheUpdatePackagesAsyncMethod()
            {
                _mockEntitiesContext = GetMock<IEntitiesContext>();
                _mockPackageService = GetMock<IPackageService>();

                _mockDatabase = GetMock<IDatabase>();
                _mockEntitiesContext
                    .Setup(x => x.GetDatabase())
                    .Returns(_mockDatabase.Object)
                    .Verifiable();

                _mockTransaction = new Mock<IDbContextTransaction>();
            }

            public static IEnumerable<object[]> SetListed_Data => 
                MemberDataHelper.AsDataSet(null, false, true);

            public static IEnumerable<object[]> ThrowsIfNullOrEmptyPackages_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.AsDataSet(null, new Package[0]),
                    SetListed_Data);

            [Theory]
            [MemberData(nameof(ThrowsIfNullOrEmptyPackages_Data))]
            public async Task ThrowsIfNullOrEmptyPackages(IReadOnlyList<Package> packages, bool? setListed)
            {
                var service = Get<PackageUpdateService>();
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.UpdatePackagesAsync(packages, setListed, _mockTransaction.Object));
            }

            public enum PackageLatestState
            {
                Not,
                Latest,
                LatestStable,
                LatestSemVer2,
                LatestStableSemVer2
            }

            public static IEnumerable<object[]> PackageCombinationsAndSetListed_Data
            {
                get
                {
                    foreach (var latestState in Enum.GetValues(typeof(PackageLatestState)).Cast<PackageLatestState>())
                    {
                        foreach (var listed in new[] { false, true })
                        {
                            if (latestState != PackageLatestState.Not && !listed)
                            {
                                continue;
                            }

                            foreach (var setListed in new[] { (bool?)null, false, true })
                            {
                                yield return MemberDataHelper.AsData(latestState, listed, setListed);
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(PackageCombinationsAndSetListed_Data))]
            public Task ThrowsWhenSqlQueryFails(PackageLatestState latestState, bool listed, bool? setListed)
            {
                var packages = GetPackagesForTest(latestState, listed);
                return Assert.ThrowsAsync<InvalidOperationException>(() => SetupAndInvokeMethod(packages, setListed, false));
            }

            [Theory]
            [MemberData(nameof(PackageCombinationsAndSetListed_Data))]
            public async Task SuccessfullyUpdatesPackages(PackageLatestState latestState, bool listed, bool? setListed)
            {
                var packages = GetPackagesForTest(latestState, listed);
                var expectedListed = packages.ToDictionary(p => p.Key, p => setListed ?? p.Listed);

                await SetupAndInvokeMethod(packages, setListed, true);

                foreach (var package in packages)
                {
                    Assert.Equal(expectedListed[package.Key], package.Listed);
                }

                _mockPackageService.Verify();
                _mockEntitiesContext.Verify();
                _mockDatabase.Verify();
                _mockTransaction.Verify();
            }

            private Task SetupAndInvokeMethod(IReadOnlyList<Package> packages, bool? setListed, bool sqlQuerySucceeds)
            {
                // Fallback UpdateIsLatestAsync setup
                // The latter setups will override this one if they apply
                _mockPackageService
                    .Setup(x => x.UpdateIsLatestAsync(It.IsAny<PackageRegistration>(), It.IsAny<bool>()))
                    .Throws(new Exception($"Unexpected {nameof(IPackageService.UpdateIsLatestAsync)} call!"));

                if (setListed.HasValue)
                {
                    foreach (var packagesByRegistration in packages.GroupBy(p => p.PackageRegistration))
                    {
                        if (!setListed.Value && !packagesByRegistration.Any(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
                        {
                            continue;
                        }

                        _mockPackageService
                            .Setup(x => x.UpdateIsLatestAsync(packagesByRegistration.Key, false))
                            .Returns(Task.CompletedTask)
                            .Verifiable();
                    }

                    _mockEntitiesContext
                        .Setup(x => x.SaveChangesAsync())
                        .Returns(Task.FromResult(0))
                        .Verifiable();
                }
                else
                {
                    _mockEntitiesContext
                        .Setup(x => x.SaveChangesAsync())
                        .Throws(new Exception($"Unexpected {nameof(IEntitiesContext.SaveChangesAsync)} call!"));
                }

                var packageKeyStrings = string.Join(
                    ", ", 
                    packages
                        .Select(p => p.Key)
                        .OrderBy(k => k));

                var expectedQuery = $@"
UPDATE [dbo].Packages
SET LastEdited = GETUTCDATE(), LastUpdated = GETUTCDATE()
WHERE [Key] IN ({packageKeyStrings})";

                _mockDatabase
                    .Setup(x => x.ExecuteSqlCommandAsync(It.Is<string>(q => q != expectedQuery)))
                    .Throws(new Exception($"Unexpected {nameof(IDatabase.ExecuteSqlCommandAsync)} call!"));

                _mockDatabase
                    .Setup(x => x.ExecuteSqlCommandAsync(expectedQuery))
                    .Returns(Task.FromResult(sqlQuerySucceeds ? packages.Count() * 2 : 0))
                    .Verifiable();

                if (sqlQuerySucceeds)
                {
                    _mockTransaction
                        .Setup(x => x.Commit())
                        .Verifiable();
                }

                return Get<PackageUpdateService>()
                    .UpdatePackagesAsync(packages, setListed, _mockTransaction.Object);
            }

            private IReadOnlyList<Package> GetPackagesForTest(PackageLatestState latestState, bool listed)
            {
                var registration = new PackageRegistration
                {
                    Id = "updatePackagesAsyncTest"
                };

                Package unselectedPackage;
                if (latestState == PackageLatestState.Not)
                {
                    unselectedPackage = new Package
                    {
                        Key = 1,
                        Version = "3.0.0",
                        PackageRegistration = registration,
                        IsLatest = true,
                        IsLatestStable = true,
                        IsLatestSemVer2 = true,
                        IsLatestStableSemVer2 = true,
                        Listed = true
                    };
                }
                else
                {
                    unselectedPackage = new Package
                    {
                        Key = 1,
                        Version = "1.0.0",
                        PackageRegistration = registration,
                        IsLatest = false,
                        IsLatestStable = false,
                        IsLatestSemVer2 = false,
                        IsLatestStableSemVer2 = false,
                        Listed = true
                    };
                }

                registration.Packages.Add(unselectedPackage);

                var selectedListedPackage = new Package
                {
                    Key = 2,
                    Version = "2.0.0",
                    PackageRegistration = registration,
                    Listed = true
                };

                registration.Packages.Add(selectedListedPackage);

                var selectedUnlistedPackage = new Package
                {
                    Key = 3,
                    Version = "2.1.0",
                    PackageRegistration = registration,
                    Listed = false
                };

                registration.Packages.Add(selectedUnlistedPackage);

                var selectedMaybeLatestPackage = new Package
                {
                    Key = 4,
                    Version = "2.5.0",
                    PackageRegistration = registration,
                    Listed = listed
                };

                registration.Packages.Add(selectedMaybeLatestPackage);

                switch (latestState)
                {
                    case PackageLatestState.Latest:
                        selectedMaybeLatestPackage.IsLatest = true;
                        break;
                    case PackageLatestState.LatestStable:
                        selectedMaybeLatestPackage.IsLatestStable = true;
                        break;
                    case PackageLatestState.LatestSemVer2:
                        selectedMaybeLatestPackage.IsLatestSemVer2 = true;
                        break;
                    case PackageLatestState.LatestStableSemVer2:
                        selectedMaybeLatestPackage.IsLatestStableSemVer2 = true;
                        break;
                    default:
                        break;
                }

                return new[]
                {
                    selectedListedPackage,
                    selectedUnlistedPackage,
                    selectedMaybeLatestPackage
                };
            }
        }
    }
}
