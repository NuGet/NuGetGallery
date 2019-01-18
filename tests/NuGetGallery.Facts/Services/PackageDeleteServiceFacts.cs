// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class PackageDeleteServiceFacts
    {
        private static readonly string _packageHashForTests = "NzMzMS1QNENLNEczSDQ1SA==";

        private static IPackageDeleteService CreateService(
            Mock<IEntityRepository<Package>> packageRepository = null,
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<PackageDelete>> packageDeletesRepository = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IPackageService> packageService = null,
            Mock<IIndexingService> indexingService = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IAuditingService> auditingService = null,
            Mock<IPackageDeleteConfiguration> config = null,
            Mock<IStatisticsService> statisticsService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<ISymbolPackageFileService> symbolPackageFileService = null,
            Mock<ISymbolPackageService> symbolPackageService = null,
            Mock<IEntityRepository<SymbolPackage>> symbolPackageRepository = null,
            Mock<ICoreLicenseFileService> coreLicenseFileService = null,
            Action<Mock<TestPackageDeleteService>> setup = null,
            bool useRealConstructor = false)
        {
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageDeletesRepository = packageDeletesRepository ?? new Mock<IEntityRepository<PackageDelete>>();

            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            var database = new Mock<IDatabase>();
            database.Setup(x => x.BeginTransaction()).Returns(() => new Mock<IDbContextTransaction>().Object);
            entitiesContext.Setup(m => m.GetDatabase()).Returns(database.Object);

            packageService = packageService ?? new Mock<IPackageService>();
            indexingService = indexingService ?? new Mock<IIndexingService>();
            packageFileService = packageFileService ?? new Mock<IPackageFileService>();

            auditingService = auditingService ?? new Mock<IAuditingService>();

            config = config ?? new Mock<IPackageDeleteConfiguration>();

            statisticsService = statisticsService ?? new Mock<IStatisticsService>();

            telemetryService = telemetryService ?? new Mock<ITelemetryService>();

            symbolPackageFileService = symbolPackageFileService ?? new Mock<ISymbolPackageFileService>();
            symbolPackageService = symbolPackageService ?? new Mock<ISymbolPackageService>();
            symbolPackageRepository = symbolPackageRepository ?? new Mock<IEntityRepository<SymbolPackage>>();
            coreLicenseFileService = coreLicenseFileService ?? new Mock<ICoreLicenseFileService>();

            if (useRealConstructor)
            {
                return new PackageDeleteService(
                    packageRepository.Object,
                    packageRegistrationRepository.Object,
                    packageDeletesRepository.Object,
                    entitiesContext.Object,
                    packageService.Object,
                    indexingService.Object,
                    packageFileService.Object,
                    auditingService.Object,
                    config.Object,
                    statisticsService.Object,
                    telemetryService.Object,
                    symbolPackageFileService.Object,
                    symbolPackageService.Object,
                    symbolPackageRepository.Object,
                    coreLicenseFileService.Object);
            }
            else
            {
                var packageDeleteService = new Mock<TestPackageDeleteService>(
                    packageRepository.Object,
                    packageRegistrationRepository.Object,
                    packageDeletesRepository.Object,
                    entitiesContext.Object,
                    packageService.Object,
                    indexingService.Object,
                    packageFileService.Object,
                    auditingService.Object,
                    config.Object,
                    statisticsService.Object,
                    telemetryService.Object,
                    symbolPackageFileService.Object,
                    symbolPackageService.Object,
                    symbolPackageRepository.Object,
                    coreLicenseFileService.Object);

                packageDeleteService.CallBase = true;

                if (setup != null)
                {
                    setup(packageDeleteService);
                }

                return packageDeleteService.Object;
            }
        }

        public class TestPackageDeleteService
            : PackageDeleteService
        {
            public PackageAuditRecord LastAuditRecord { get; set; }

            public TestPackageDeleteService(
                IEntityRepository<Package> packageRepository,
                IEntityRepository<PackageRegistration> packageRegistrationRepository,
                IEntityRepository<PackageDelete> packageDeletesRepository,
                IEntitiesContext entitiesContext,
                IPackageService packageService,
                IIndexingService indexingService,
                IPackageFileService packageFileService,
                IAuditingService auditingService,
                IPackageDeleteConfiguration config,
                IStatisticsService statisticsService,
                ITelemetryService telemetryService,
                ISymbolPackageFileService symbolPackageFileService,
                ISymbolPackageService symbolPackageService,
                IEntityRepository<SymbolPackage> symbolPackageRepository,
                ICoreLicenseFileService coreLicenseFileService) : base(
                    packageRepository,
                    packageRegistrationRepository,
                    packageDeletesRepository,
                    entitiesContext,
                    packageService,
                    indexingService,
                    packageFileService,
                    auditingService,
                    config,
                    statisticsService,
                    telemetryService,
                    symbolPackageFileService,
                    symbolPackageService,
                    symbolPackageRepository,
                    coreLicenseFileService)
            {
            }

            protected override async Task ExecuteSqlCommandAsync(IDatabase database, string sql, params object[] parameters)
            {
                await TestExecuteSqlCommandAsync(database, sql, parameters);
            }

            public virtual Task TestExecuteSqlCommandAsync(IDatabase database, string sql, params object[] parameters)
            {
                // do nothing - this method solely exists to make verifying SQL queries possible
                return Task.FromResult(0);
            }

            protected override PackageAuditRecord CreateAuditRecord(Package package, PackageRegistration packageRegistration, AuditedPackageAction action, string reason)
            {
                LastAuditRecord = base.CreateAuditRecord(package, packageRegistration, action, reason);
                return LastAuditRecord;
            }
        }

        public class TheConstructor
        {
            [Fact]
            public void RejectsHourLimitWithMaximumDownloadsLessThanStatisticsUpdateFrequencyInHours()
            {
                // Arrange
                var config = new Mock<IPackageDeleteConfiguration>();
                config.Setup(x => x.StatisticsUpdateFrequencyInHours).Returns(24);
                config.Setup(x => x.HourLimitWithMaximumDownloads).Returns(23);

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(
                    () => CreateService(config: config, useRealConstructor: true));
                Assert.Contains(
                    "StatisticsUpdateFrequencyInHours must be less than HourLimitWithMaximumDownloads.",
                    exception.Message);
            }
        }

        public class TheCanPackageBeDeletedByUserAsyncMethod
        {
            private readonly Package _package;
            private ReportPackageReason? _reason;
            private PackageDeleteDecision? _decision;
            private readonly StatisticsPackagesReport _packageReport;
            private readonly Mock<IPackageDeleteConfiguration> _config;
            private readonly Mock<IStatisticsService> _statisticsService;
            private readonly Mock<ITelemetryService> _telemetryService;
            private readonly IPackageDeleteService _target;

            public TheCanPackageBeDeletedByUserAsyncMethod()
            {
                _package = new Package
                {
                    PackageStatusKey = PackageStatus.Available,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "NuGet.Versioning",
                        DownloadCount = 0,
                        IsLocked = false,
                    },
                    NormalizedVersion = "4.5.0",
                    DownloadCount = 0,
                    Created = DateTime.UtcNow,
                };
                _reason = ReportPackageReason.ReleasedInPublicByAccident;
                _decision = PackageDeleteDecision.DeletePackage;
                _packageReport = new StatisticsPackagesReport
                {
                    Facts = new List<StatisticsFact>()
                    {
                        MakeFact("4.5.0", 0),
                    },
                };

                _config = new Mock<IPackageDeleteConfiguration>();
                _config.Setup(x => x.AllowUsersToDeletePackages).Returns(true);
                _config.Setup(x => x.MaximumDownloadsForPackageId).Returns(125000);
                _config.Setup(x => x.StatisticsUpdateFrequencyInHours).Returns(24);
                _config.Setup(x => x.HourLimitWithMaximumDownloads).Returns(72);
                _config.Setup(x => x.MaximumDownloadsForPackageVersion).Returns(100);

                _statisticsService = new Mock<IStatisticsService>();
                _statisticsService.Setup(x => x.LastUpdatedUtc).Returns(DateTime.UtcNow);
                _statisticsService
                    .Setup(x => x.GetPackageDownloadsByVersion(It.IsAny<string>()))
                    .ReturnsAsync(() => _packageReport);

                _telemetryService = new Mock<ITelemetryService>();

                _target = CreateService(
                    config: _config,
                    statisticsService: _statisticsService,
                    telemetryService: _telemetryService);
            }

            [Fact]
            public async Task AllowsTheDeleteWhenAllChecksPass()
            {
                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.True(actual, "The delete should have been allowed.");
                VerifyOutcome(UserPackageDeleteOutcome.Accepted);
            }

            [Fact]
            public async Task AllowsTheDeleteWhenMaximumIdDownloadsAreNotConfigured()
            {
                _package.PackageRegistration.DownloadCount = 125001;
                _config.Setup(x => x.MaximumDownloadsForPackageId).Returns((int?)null);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.True(actual, "The delete should have been allowed.");
                VerifyOutcome(UserPackageDeleteOutcome.Accepted);
            }

            [Fact]
            public async Task AllowsTheDeleteWhenMaximumVersionDownloadsAreNotConfigured()
            {
                _package.Created = DateTime.UtcNow.AddHours(-25);
                _package.DownloadCount = 101;
                _config.Setup(x => x.MaximumDownloadsForPackageVersion).Returns((int?)null);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.True(actual, "The delete should have been allowed.");
                VerifyOutcome(UserPackageDeleteOutcome.Accepted);
            }

            [Fact]
            public async Task AllowDeletesWhenTheIdReportDoesNotHaveTooManyDownloadsButStatisticsAreStale()
            {
                MakeStatisticsStale();
                _packageReport.Facts.Add(MakeFact("3.3.0", 124000));
                _packageReport.Facts.Add(MakeFact("3.4.0", 999));

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.True(actual, "The delete should have been allowed.");
                VerifyOutcome(UserPackageDeleteOutcome.Accepted);
            }

            [Fact]
            public async Task AllowsDeletesWhenTimeRangesAreNotDefined()
            {
                _package.Created = DateTime.UtcNow.AddHours(-73);
                _config.Setup(x => x.HourLimitWithMaximumDownloads).Returns((int?)null);
                _config.Setup(x => x.StatisticsUpdateFrequencyInHours).Returns((int?)null);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.True(actual, "The delete should have been allowed.");
                VerifyOutcome(UserPackageDeleteOutcome.Accepted);
            }

            [Fact]
            public async Task AllowsDeleteWhenCreatedAfterEarlyTimeRangeAndIdDownloadsAreLowEnough()
            {
                _package.Created = DateTime.UtcNow.AddHours(-25);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.True(actual, "The delete should have been allowed.");
                VerifyOutcome(UserPackageDeleteOutcome.Accepted);
            }

            [Fact]
            public async Task DoesNotAllowDeleteWhenCreatedAfterEarlyTimeRangeAndLateIsUndefined()
            {
                _package.Created = DateTime.UtcNow.AddHours(-25);
                _config.Setup(x => x.HourLimitWithMaximumDownloads).Returns((int?)null);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed if the late time range is not defined.");
                VerifyOutcome(UserPackageDeleteOutcome.TooLate);
            }

            [Fact]
            public async Task DoesNotAllowDeleteWhenCreatedAfterLateTimeRangeAndEarlyIsUndefined()
            {
                _package.Created = DateTime.UtcNow.AddHours(-73);
                _config.Setup(x => x.StatisticsUpdateFrequencyInHours).Returns((int?)null);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed even if the early time range is not defined.");
                VerifyOutcome(UserPackageDeleteOutcome.TooLate);
            }

            [Fact]
            public async Task DoesNotAllowDeleteWhenCreatedAfterLateTimeRange()
            {
                _package.Created = DateTime.UtcNow.AddHours(-73);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed after the late time range.");
                VerifyOutcome(UserPackageDeleteOutcome.TooLate);
            }

            [Fact]
            public async Task DoesNotAllowDeletesOnDeletedPackages()
            {
                _package.PackageStatusKey = PackageStatus.Deleted;

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed on a deleted package.");
                VerifyOutcome(UserPackageDeleteOutcome.AlreadyDeleted);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheIdIsLocked()
            {
                _package.PackageRegistration.IsLocked = true;

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when the package is locked.");
                VerifyOutcome(UserPackageDeleteOutcome.LockedRegistration);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheFeatureIsDisabled()
            {
                _config.Setup(x => x.AllowUsersToDeletePackages).Returns(false);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when the feature is disabled.");
                _telemetryService.Verify(
                    x => x.TrackUserPackageDeleteChecked(It.IsAny<UserPackageDeleteEvent>(), It.IsAny<UserPackageDeleteOutcome>()),
                    Times.Never);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheStatisticsAreStale()
            {
                MakeStatisticsStale();
                _package.Created = DateTime.UtcNow.AddHours(-25);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when statistics are stale.");
                VerifyOutcome(UserPackageDeleteOutcome.StaleStatistics);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheStatisticsUpdateTimeIsNull()
            {
                _package.Created = DateTime.UtcNow.AddHours(-25);
                _statisticsService.Setup(x => x.LastUpdatedUtc).Returns((DateTime?)null);

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when statistics are stale.");
                VerifyOutcome(UserPackageDeleteOutcome.StaleStatistics);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheIdHasTooManyDownloads()
            {
                _package.PackageRegistration.DownloadCount = 125001;

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when the ID has too many downloads.");
                VerifyOutcome(UserPackageDeleteOutcome.TooManyIdDatabaseDownloads);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheIdReportHasTooManyDownloads()
            {
                _packageReport.Facts.Add(MakeFact("3.3.0", 124000));
                _packageReport.Facts.Add(MakeFact("3.4.0", 1001));

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when the ID report has too many downloads.");
                VerifyOutcome(UserPackageDeleteOutcome.TooManyIdReportDownloads);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheVersionTooManyDownloads()
            {
                _package.Created = DateTime.UtcNow.AddHours(-25);
                _package.DownloadCount = 101;

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when the version has too many downloads.");
                VerifyOutcome(UserPackageDeleteOutcome.TooManyVersionDatabaseDownloads);
            }

            [Fact]
            public async Task DoesNotAllowDeletesWhenTheVersionReportTooManyDownloads()
            {
                _package.Created = DateTime.UtcNow.AddHours(-25);
                _packageReport.Facts.Add(MakeFact("4.5.0", 50));
                _packageReport.Facts.Add(MakeFact("4.5.0", 51));

                var actual = await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                Assert.False(actual, "Deletes should not be allowed when the version report has too many downloads.");
                VerifyOutcome(UserPackageDeleteOutcome.TooManyVersionReportDownloads);
            }

            [Fact]
            public async Task DoesNotEmitTelemetryIfReasonIsNull()
            {
                _reason = null;

                await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                _telemetryService.Verify(
                    x => x.TrackUserPackageDeleteChecked(It.IsAny<UserPackageDeleteEvent>(), It.IsAny<UserPackageDeleteOutcome>()),
                    Times.Never);
            }

            [Fact]
            public async Task EmitsTelemetryEvenIfDecisionIsNull()
            {
                _decision = null;

                await _target.CanPackageBeDeletedByUserAsync(_package, _reason, _decision);

                _telemetryService.Verify(
                    x => x.TrackUserPackageDeleteChecked(It.IsAny<UserPackageDeleteEvent>(), It.IsAny<UserPackageDeleteOutcome>()),
                    Times.Once);
            }

            private void VerifyOutcome(UserPackageDeleteOutcome outcome)
            {
                _telemetryService.Verify(
                    x => x.TrackUserPackageDeleteChecked(It.IsAny<UserPackageDeleteEvent>(), outcome),
                    Times.Once);
                _telemetryService.Verify(
                    x => x.TrackUserPackageDeleteChecked(It.IsAny<UserPackageDeleteEvent>(), It.IsAny<UserPackageDeleteOutcome>()),
                    Times.Once);
            }

            private static StatisticsFact MakeFact(string version, int downloads)
            {
                return new StatisticsFact(
                    new Dictionary<string, string>
                    {
                        { "Version", version },
                    },
                    downloads);
            }

            private void MakeStatisticsStale()
            {
                _statisticsService.Setup(x => x.LastUpdatedUtc).Returns(DateTime.UtcNow.AddHours(-25));
            }
        }

        public class TheSoftDeletePackagesAsyncMethod
        {
            [Fact]
            public async Task WillIncreaseTheDatabaseCommandTimeout()
            {
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var entitiesContext = new Mock<IEntitiesContext>();
                var service = CreateService(packageRepository: packageRepository, entitiesContext: entitiesContext);

                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                entitiesContext.Verify(x => x.SetCommandTimeout(300));
                Mock.Get(service).Verify();
            }

            [Fact]
            public async Task WillMarkThePackageAsUnlisted()
            {
                var packageDeletesRepo = new Mock<IEntityRepository<PackageDelete>>();
                var service = CreateService(packageDeletesRepository: packageDeletesRepo);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.SoftDeletePackagesAsync(new[] { package }, user, reason, signature);

                Assert.False(package.Listed);
            }

            [Fact]
            public async Task WillInsertNewRecordIntoThePackageDeletesRepository()
            {
                var packageDeletesRepo = new Mock<IEntityRepository<PackageDelete>>();
                var service = CreateService(packageDeletesRepository: packageDeletesRepo);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.SoftDeletePackagesAsync(new[] { package }, user, reason, signature);

                packageDeletesRepo.Verify(x => x.InsertOnCommit(It.Is<PackageDelete>(p => p.Packages.Contains(package) && p.DeletedBy == user && p.Reason == reason && p.Signature == signature)));
                packageDeletesRepo.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task WillUpdateAllRepositories()
            {
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var packageDeleteRepository = new Mock<IEntityRepository<PackageDelete>>();
                var symbolPackageRepository = new Mock<IEntityRepository<SymbolPackage>>();
                var service = CreateService(packageRepository: packageRepository, packageDeletesRepository: packageDeleteRepository, symbolPackageRepository: symbolPackageRepository);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);
                
                packageRepository.Verify(x => x.CommitChangesAsync());
                symbolPackageRepository.Verify(x => x.CommitChangesAsync());
                packageDeleteRepository.Verify(x => x.InsertOnCommit(It.IsAny<PackageDelete>()));
                packageDeleteRepository.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task WillUpdatePackageStatusToDeleted()
            {
                var packageService = new Mock<IPackageService>();
                var service = CreateService(packageService: packageService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                packageService.Verify(x => x.UpdatePackageStatusAsync(package, PackageStatus.Deleted, false));
            }

            [Fact]
            public async Task WillUpdateSymbolPackageStatusToDeleted()
            {
                var symbolPackageService = new Mock<ISymbolPackageService>();
                var service = CreateService(symbolPackageService: symbolPackageService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                var symbolPackage = new SymbolPackage()
                {
                    Package = package,
                    StatusKey = PackageStatus.Available
                };
                package.SymbolPackages.Add(symbolPackage);
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                symbolPackageService.Verify(x => x.UpdateStatusAsync(symbolPackage, PackageStatus.Deleted, false));
            }

            [Fact]
            public async Task WillUpdateTheIndexingService()
            {
                var indexingService = new Mock<IIndexingService>();
                var service = CreateService(indexingService: indexingService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                indexingService.Verify(x => x.UpdateIndex(true));
            }

            [Fact]
            public async Task WillUnlinkDeprecationsThatRecommendThePackage()
            {
                var service = CreateService();
                var deprecatedPackageRegistration = new PackageRegistration();
                var deprecatedPackage = new Package { PackageRegistration = deprecatedPackageRegistration, Version = "1.0.0" };
                deprecatedPackageRegistration.Packages.Add(deprecatedPackage);

                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);

                var deprecation = new PackageDeprecation { Package = deprecatedPackage, AlternatePackage = package };
                deprecatedPackage.Deprecations.Add(deprecation);
                package.AlternativeOf.Add(deprecation);

                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                Assert.Empty(package.AlternativeOf);
                Assert.Null(deprecation.AlternatePackage);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustThePublicPackageFile()
            {
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                packageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustThePublicSymbolPackageFile()
            {
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                symbolPackageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustTheValidationPackageFile()
            {
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                packageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustTheValidationSymbolPackageFile()
            {
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                symbolPackageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteBothThePublicAndValidationPackageFile()
            {
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);
                packageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                packageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Exactly(2));
                packageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteBothThePublicAndValidationSymbolPackageFile()
            {
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);
                symbolPackageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                symbolPackageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Exactly(2));
                symbolPackageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillDeleteReadMeFiles()
            {
                var packageFileService = new Mock<IPackageFileService>();

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);
                
                packageFileService.Verify(x => x.DeleteReadMeMdFileAsync(package), Times.Once);
            }

            [Fact]
            public async Task WillCreateAuditRecordUsingAuditService()
            {
                var auditingService = new Mock<IAuditingService>();
                var service = CreateService(auditingService: auditingService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.SoftDeletePackagesAsync(new[] { package }, user, reason, signature);

                var testService = service as TestPackageDeleteService;
                Assert.Equal(package.PackageRegistration.Id, testService.LastAuditRecord.Id);
                Assert.Equal(package.Version, testService.LastAuditRecord.Version);
                auditingService.Verify(x => x.SaveAuditRecordAsync(testService.LastAuditRecord));
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                var telemetryService = new Mock<ITelemetryService>();
                var service = CreateService(telemetryService: telemetryService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.SoftDeletePackagesAsync(new[] { package }, user, reason, signature);

                telemetryService.Verify(x => x.TrackPackageDelete(package, false));
            }
        }

        public class TheHardDeletePackagesAsyncMethod
        {
            [Fact]
            public async Task WillIncreaseTheDatabaseCommandTimeout()
            {
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var entitiesContext = new Mock<IEntitiesContext>();
                var service = CreateService(packageRepository: packageRepository, entitiesContext: entitiesContext);

                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                entitiesContext.Verify(x => x.SetCommandTimeout(300));
                Mock.Get(service).Verify();
            }

            [Fact]
            public async Task WillDeletePackageAndRelatedEntities()
            {
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var entitiesContext = new Mock<IEntitiesContext>();
                var service = CreateService(packageRepository: packageRepository, entitiesContext: entitiesContext, setup: svc =>
                {
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                });
                var packageRegistration = new PackageRegistration();
                packageRegistration.Packages.Add(new Package { Key = 124, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests });

                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                Assert.DoesNotContain(package, packageRegistration.Packages);
                packageRepository.Verify(x => x.DeleteOnCommit(package));
                packageRepository.Verify(x => x.CommitChangesAsync());
                Mock.Get(service).Verify();
            }

            [Fact]
            public async Task WillNotDeletePackageRegistrationWhenNoPackagesLeftAndDeleteEmptyPackageRegistrationFalse()
            {
                var deleteEmptyPackageRegistration = false;

                var ranDeleteQuery = false;
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var entitiesContext = new Mock<IEntitiesContext>();
                var service = CreateService(packageRepository: packageRepository, entitiesContext: entitiesContext, setup: svc =>
                {
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();

                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), PackageDeleteService.DeletePackageRegistrationQuery, It.IsAny<SqlParameter>())).Callback(() => ranDeleteQuery = true).Returns(Task.FromResult(0));
                });
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, deleteEmptyPackageRegistration);

                Assert.Equal(0, packageRegistration.Packages.Count);
                Assert.DoesNotContain(package, packageRegistration.Packages);
                packageRepository.Verify(x => x.DeleteOnCommit(package));
                packageRepository.Verify(x => x.CommitChangesAsync());
                Assert.False(ranDeleteQuery);
                Mock.Get(service).Verify();
            }

            [Fact]
            public async Task WillDeletePackageRegistrationWhenNoPackagesLeftAndDeleteEmptyPackageRegistrationTrue()
            {
                var deleteEmptyPackageRegistration = true;

                var packageRepository = new Mock<IEntityRepository<Package>>();
                var entitiesContext = new Mock<IEntitiesContext>();
                var service = CreateService(packageRepository: packageRepository, entitiesContext: entitiesContext, setup: svc =>
                {
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pa FROM PackageAuthors pa JOIN Packages p ON p.[Key] = pa.PackageKey WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pd FROM PackageDependencies pd JOIN Packages p ON p.[Key] = pd.PackageKey WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), "DELETE pf FROM PackageFrameworks pf JOIN Packages p ON p.[Key] = pf.Package_Key WHERE p.[Key] = @key", It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();

                    svc.Setup(x => x.TestExecuteSqlCommandAsync(It.IsAny<IDatabase>(), PackageDeleteService.DeletePackageRegistrationQuery, It.IsAny<SqlParameter>())).Returns(Task.FromResult(0)).Verifiable();
                });
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, deleteEmptyPackageRegistration);

                Assert.Equal(0, packageRegistration.Packages.Count);
                Assert.DoesNotContain(package, packageRegistration.Packages);
                packageRepository.Verify(x => x.DeleteOnCommit(package));
                packageRepository.Verify(x => x.CommitChangesAsync());
                Mock.Get(service).Verify();
            }

            [Fact]
            public async Task WillUpdatePackageLatestVersions()
            {
                var packageService = new Mock<IPackageService>();
                var service = CreateService(packageService: packageService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                packageService.Verify(x => x.UpdateIsLatestAsync(packageRegistration, false));
            }

            [Fact]
            public async Task WillUpdateTheIndexingService()
            {
                var indexingService = new Mock<IIndexingService>();
                var service = CreateService(indexingService: indexingService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                indexingService.Verify(x => x.UpdateIndex(true));
            }

            [Fact]
            public async Task WillUnlinkDeprecationsThatRecommendThePackage()
            {
                var service = CreateService();
                var deprecatedPackageRegistration = new PackageRegistration();
                var deprecatedPackage = new Package { PackageRegistration = deprecatedPackageRegistration, Version = "1.0.0" };
                deprecatedPackageRegistration.Packages.Add(deprecatedPackage);

                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);

                var deprecation = new PackageDeprecation { Package = deprecatedPackage, AlternatePackage = package };
                deprecatedPackage.Deprecations.Add(deprecation);
                package.AlternativeOf.Add(deprecation);

                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                Assert.Empty(package.AlternativeOf);
                Assert.Null(deprecation.AlternatePackage);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustThePublicPackageFile()
            {
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                packageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustThePublicSymbolPackageFile()
            {
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                symbolPackageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustTheValidationPackageFile()
            {
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                packageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteJustTheValidationSymbolPackageFile()
            {
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                symbolPackageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteBothThePublicAndValidationPackageFile()
            {
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);
                packageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                packageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Exactly(2));
                packageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillBackupAndDeleteBothThePublicAndValidationSymbolPackageFile()
            {
                var symbolPackageFileService = new Mock<ISymbolPackageFileService>();
                symbolPackageFileService.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);
                symbolPackageFileService.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>()))
                    .ReturnsAsync(Stream.Null);

                var service = CreateService(symbolPackageFileService: symbolPackageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                symbolPackageFileService.Verify(
                    x => x.StorePackageFileInBackupLocationAsync(package, It.IsAny<Stream>()),
                    Times.Exactly(2));
                symbolPackageFileService.Verify(
                    x => x.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
                symbolPackageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version),
                    Times.Once);
            }

            [Fact]
            public async Task WillDeleteReadMeFile()
            {
                var packageFileService = new Mock<IPackageFileService>();

                var service = CreateService(packageFileService: packageFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.HardDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty, false);

                packageFileService.Verify(x => x.DeleteReadMeMdFileAsync(package), Times.Once);
            }

            [Fact]
            public async Task WillCreateAuditRecordUsingAuditService()
            {
                var auditingService = new Mock<IAuditingService>();
                var service = CreateService(auditingService: auditingService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { Key = 123, PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.HardDeletePackagesAsync(new[] { package }, user, reason, signature, false);

                var testService = service as TestPackageDeleteService;
                Assert.Equal(package.PackageRegistration.Id, testService.LastAuditRecord.Id);
                Assert.Equal(package.Version, testService.LastAuditRecord.Version);
                auditingService.Verify(x => x.SaveAuditRecordAsync(testService.LastAuditRecord));
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                var telemetryService = new Mock<ITelemetryService>();
                var service = CreateService(telemetryService: telemetryService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.HardDeletePackagesAsync(new[] { package }, user, reason, signature, deleteEmptyPackageRegistration: false);

                telemetryService.Verify(x => x.TrackPackageDelete(package, true));
            }
        }

        public class TheReflowHardDeletedPackagesAsyncMethod
        {
            [Fact]
            public Task FailsIfPackageExists()
            {
                var id = "a";
                var version = "1.0.0";
                return ReflowHardDeletedPackage(id, version, id, version, false);
            }

            [Fact]
            public Task SucceedsIfRegistrationExistsButNotPackage()
            {
                var id = "a";
                var version = "1.0.0";
                var existingVersion = "2.0.0";
                return ReflowHardDeletedPackage(id, version, id, existingVersion, true);
            }

            [Fact]
            public Task SucceedsIfRegistrationDoesNotExist()
            {
                var id = "a";
                var version = "1.0.0";
                var existingId = "b";
                var existingVersion = "2.0.0";
                return ReflowHardDeletedPackage(id, version, existingId, existingVersion, true);
            }

            private async Task ReflowHardDeletedPackage(string id, string version, string existingId, string existingVersion, bool succeeds)
            {
                var packageRegistrationKey = 1;
                var packageRegistration = new PackageRegistration { Key = packageRegistrationKey, Id = existingId };
                var package = new Package { PackageRegistrationKey = packageRegistrationKey, NormalizedVersion = existingVersion };

                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                packageRegistrationRepository.Setup(x => x.GetAll()).Returns(new PackageRegistration[] { packageRegistration }.AsQueryable());

                var packageRepository = new Mock<IEntityRepository<Package>>();
                packageRepository.Setup(x => x.GetAll()).Returns(new Package[] { package }.AsQueryable());

                var auditingService = new Mock<IAuditingService>();

                var telemetryService = new Mock<ITelemetryService>();

                var service = CreateService(
                    packageRepository: packageRepository,
                    packageRegistrationRepository: packageRegistrationRepository,
                    auditingService: auditingService,
                    telemetryService: telemetryService);
                
                if (succeeds)
                {
                    await service.ReflowHardDeletedPackageAsync(id, version);
                }
                else
                {
                    await Assert.ThrowsAsync<UserSafeException>(() => service.ReflowHardDeletedPackageAsync(id, version));
                }
                
                auditingService.Verify(
                    x => x.SaveAuditRecordAsync(It.IsAny<AuditRecord>()),
                    succeeds ? Times.Once() : Times.Never());

                telemetryService.Verify(
                    x => x.TrackPackageHardDeleteReflow(id, version),
                    succeeds ? Times.Once() : Times.Never());
            }
        }
    }
}
