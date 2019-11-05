// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackageUpdateServiceFacts
    {
        public enum PackageLatestState
        {
            Not,
            Latest,
            LatestStable,
            LatestSemVer2,
            LatestStableSemVer2
        }

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

                SetLatestOfPackage(firstPackage, packageLatestState);

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
            private readonly Mock<IDatabase> _mockDatabase;
            private readonly Mock<IIndexingService> _mockIndexingService;

            public TheUpdatePackagesAsyncMethod()
            {
                _mockIndexingService = GetMock<IIndexingService>();
                _mockDatabase = GetMock<IDatabase>();
                _mockEntitiesContext = GetMock<IEntitiesContext>();
                _mockEntitiesContext
                    .Setup(x => x.GetDatabase())
                    .Returns(_mockDatabase.Object)
                    .Verifiable();
            }

            public static IEnumerable<object[]> ThrowsIfNullOrEmptyPackages_Data => MemberDataHelper.AsDataSet(null, new Package[0]);

            [Theory]
            [MemberData(nameof(ThrowsIfNullOrEmptyPackages_Data))]
            public async Task ThrowsIfNullOrEmptyPackages(IReadOnlyList<Package> packages)
            {
                var service = Get<PackageUpdateService>();
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.UpdatePackagesAsync(packages));
            }

            public static IEnumerable<object[]> PackageCombinations_Data
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

                            yield return MemberDataHelper.AsData(latestState, listed);
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(PackageCombinations_Data))]
            public Task ThrowsWhenSqlQueryFails(PackageLatestState latestState, bool listed)
            {
                var packages = GetPackagesForTest(latestState, listed);
                return Assert.ThrowsAsync<InvalidOperationException>(() => SetupAndInvokeMethod(packages, false));
            }

            [Theory]
            [MemberData(nameof(PackageCombinations_Data))]
            public async Task SuccessfullyUpdatesPackages(PackageLatestState latestState, bool listed)
            {
                var packages = GetPackagesForTest(latestState, listed);
                await SetupAndInvokeMethod(packages, true);

                _mockDatabase.Verify();
                _mockIndexingService.Verify();
            }

            [Theory]
            [MemberData(nameof(PackageCombinations_Data))]
            public async Task SuccessfullyUpdatesPackagesWithMultipleRegistrations(PackageLatestState latestState, bool listed)
            {
                var firstPackages = GetPackagesForTest(latestState, listed, 0);
                var secondPackages = GetPackagesForTest(latestState, listed, 1);
                var allPackages = firstPackages.Concat(secondPackages).ToList();
                await SetupAndInvokeMethod(allPackages, true);

                _mockDatabase.Verify();
                _mockIndexingService.Verify();
            }

            private Task SetupAndInvokeMethod(IReadOnlyList<Package> packages, bool sqlQuerySucceeds)
            {
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

                foreach (var registration in packages.Select(p => p.PackageRegistration).Distinct())
                {
                    _mockIndexingService
                        .Setup(x => x.UpdatePackage(It.Is<Package>(p => p.PackageRegistration == registration)))
                        .Verifiable();
                }

                return Get<PackageUpdateService>()
                    .UpdatePackagesAsync(packages);
            }

            private IReadOnlyList<Package> GetPackagesForTest(PackageLatestState latestState, bool listed, int number = 0)
            {
                var registration = new PackageRegistration
                {
                    Id = "updatePackagesAsyncTest" + number
                };

                Package unselectedPackage;
                if (latestState == PackageLatestState.Not)
                {
                    unselectedPackage = new Package
                    {
                        Key = 1 + number * 100,
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
                        Key = 1 + number * 100,
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
                    Key = 2 + number * 100,
                    Version = "2.0.0",
                    PackageRegistration = registration,
                    Listed = true
                };

                registration.Packages.Add(selectedListedPackage);

                var selectedUnlistedPackage = new Package
                {
                    Key = 3 + number * 100,
                    Version = "2.1.0",
                    PackageRegistration = registration,
                    Listed = false
                };

                registration.Packages.Add(selectedUnlistedPackage);

                var selectedMaybeLatestPackage = new Package
                {
                    Key = 4 + number * 100,
                    Version = "2.5.0",
                    PackageRegistration = registration,
                    Listed = listed
                };

                registration.Packages.Add(selectedMaybeLatestPackage);

                SetLatestOfPackage(selectedMaybeLatestPackage, latestState);

                return new[]
                {
                    selectedListedPackage,
                    selectedUnlistedPackage,
                    selectedMaybeLatestPackage
                };
            }
        }

        private static void SetLatestOfPackage(Package package, PackageLatestState latestState)
        {
            switch (latestState)
            {
                case PackageLatestState.Latest:
                    package.IsLatest = true;
                    break;
                case PackageLatestState.LatestStable:
                    package.IsLatestStable = true;
                    break;
                case PackageLatestState.LatestSemVer2:
                    package.IsLatestSemVer2 = true;
                    break;
                case PackageLatestState.LatestStableSemVer2:
                    package.IsLatestStableSemVer2 = true;
                    break;
            }
        }
    }
}
