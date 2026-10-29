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
            Mock<ICoreReadmeFileService> coreReadmeFileService = null, 
            Mock<IPackageDeleteBackupService> packageDeleteBackupService = null,
            Action<Mock<TestPackageDeleteService>> setup = null,
            bool useRealConstructor = false,
            Action<Mock<IDatabase>> configureDatabase = null)
        {
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageDeletesRepository = packageDeletesRepository ?? new Mock<IEntityRepository<PackageDelete>>();

            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            var database = new Mock<IDatabase>();
            database.Setup(x => x.BeginTransaction()).Returns(() => new Mock<IDbContextTransaction>().Object);
            configureDatabase?.Invoke(database);
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
            coreReadmeFileService = coreReadmeFileService ?? new Mock<ICoreReadmeFileService>();
            packageDeleteBackupService = packageDeleteBackupService ?? new Mock<IPackageDeleteBackupService>();

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
                    coreLicenseFileService.Object,
                    coreReadmeFileService.Object,
                    packageDeleteBackupService.Object);
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
                    coreLicenseFileService.Object,
                    coreReadmeFileService.Object,
                    packageDeleteBackupService.Object);

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
                ICoreLicenseFileService coreLicenseFileService,
                ICoreReadmeFileService coreReadmeFileService,
                IPackageDeleteBackupService packageDeleteBackupService) : base(
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
                    coreLicenseFileService,
                    coreReadmeFileService,
                    packageDeleteBackupService)
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
            public async Task WillDeleteEmbeddedReadmeFiles()
            {
                var coreReadmeFileService = new Mock<ICoreReadmeFileService>();

                var service = CreateService(coreReadmeFileService: coreReadmeFileService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                package.HasReadMe = true;
                package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                coreReadmeFileService.Verify(x => x.DeleteReadmeFileAsync(package.Id, package.Version), Times.Once);
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

        public class TheAdminSoftDeleteCapture
        {
            [Theory]
            [InlineData(PackageStatus.Available, true)]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.FailedValidation, true)]
            public async Task CapturesOriginalStateBeforeCleanup(PackageStatus status, bool listed)
            {
                var fixture = new CaptureFixture();
                fixture.Package.PackageStatusKey = status;
                fixture.Package.Listed = listed;
                fixture.Package.AlternativeOf.Add(new PackageDeprecation { Key = 50 });
                fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 20, StatusKey = PackageStatus.Available });
                fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 21, StatusKey = PackageStatus.Validating });
                fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 22, StatusKey = PackageStatus.Deleted });
                fixture.SymbolFiles.Setup(x => x.DownloadPackageFileAsync(fixture.Package)).ReturnsAsync(() => new MemoryStream(new byte[] { 3 }));
                fixture.SymbolFiles.Setup(x => x.DownloadValidationPackageFileAsync(fixture.Package)).ReturnsAsync(() => new MemoryStream(new byte[] { 4 }));
                fixture.SymbolFiles.Setup(x => x.StorePackageFileInBackupLocationAsync(fixture.Package, It.IsAny<Stream>()))
                    .Returns<Package, Stream>((p, s) => Task.FromResult(CryptographyService.GenerateHash(s, CoreConstants.Sha512HashAlgorithmId)));

                await fixture.DeleteAsync();

                var backup = Assert.Single(fixture.Saved);
                var deletion = Assert.Single(fixture.Deletions);
                Assert.Equal(deletion.Key, backup.PackageDeleteKey);
                Assert.Equal(deletion.DeletedOn, backup.DeletedOnUtc);
                Assert.Equal(fixture.Package.Key, backup.PackageKey);
                Assert.Equal(status, backup.PackageStatusKey);
                Assert.Equal(listed, backup.Listed);
                Assert.Equal(CaptureFixture.PublicHash, backup.PublicBackupHash);
                Assert.Equal(CaptureFixture.ValidationHash, backup.ValidationBackupHash);
                Assert.Equal(CaptureFixture.Readme, backup.ExternalReadmeMarkdown);
                Assert.Equal(new[] { 50 }, backup.IncomingDeprecationKeys);
                Assert.Equal(new[] { 20, 21 }, backup.Symbols.Select(s => s.SymbolPackageKey));
                Assert.Equal(new[] { PackageStatus.Available, PackageStatus.Validating }, backup.Symbols.Select(s => s.SymbolPackageStatusKey));
                Assert.NotEqual(backup.Symbols[0].PublicBackupHash, backup.Symbols[1].ValidationBackupHash);
                Assert.True(fixture.Events.IndexOf("identity-flush") < fixture.Events.IndexOf("save-json"));
                Assert.True(fixture.Events.IndexOf("save-json") < fixture.Events.IndexOf("cleanup"));
                Assert.True(fixture.Events.IndexOf("cleanup") < fixture.Events.IndexOf("commit"));
                fixture.Database.Verify(x => x.BeginTransaction(), Times.Once);
                fixture.Backups.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                fixture.Auditing.Verify(x => x.SaveAuditRecordAsync(It.Is<PackageAuditRecord>(a =>
                    a.PackageKey == fixture.Package.Key && a.PackageDeleteKey == deletion.Key && a.AdminInitiated == true
                    && a.Action == AuditedPackageAction.SoftDelete && a.GetResourceType() == "Package")), Times.Once);
                Assert.False(fixture.Package.Listed);
            }

            [Fact]
            public async Task OwnerDeletionDoesNotCaptureRecoveryJson()
            {
                var fixture = new CaptureFixture();

                await fixture.DeleteAsync(admin: false);

                Assert.Empty(fixture.Saved);
                fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
                fixture.Auditing.Verify(x => x.SaveAuditRecordAsync(It.Is<PackageAuditRecord>(a =>
                    a.Action == AuditedPackageAction.SoftDelete && a.PackageKey == null
                    && a.PackageDeleteKey == null && a.AdminInitiated == null)), Times.Once);
            }

            [Fact]
            public async Task CaptureFailureDoesNotRemoveAnyFilesInTheBatch()
            {
                var fixture = new CaptureFixture();
                var other = CaptureFixture.NewPackage(2);
                var error = new IOException("JSON unavailable");
                fixture.Backups.Setup(x => x.SaveAsync(It.Is<PackageDeleteBackup>(b => b.PackageKey == 2)))
                    .ThrowsAsync(error);

                Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => fixture.Service.SoftDeletePackagesAsync(
                    new[] { fixture.Package, other }, null, "reason", "admin-app", true)));

                fixture.VerifyNoCleanup();
                Assert.Equal(fixture.Package.Key, Assert.Single(fixture.Saved).PackageKey);
                Assert.Empty(fixture.Deletions);
                fixture.Backups.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                Assert.True(fixture.Package.Listed);
                Assert.True(other.Listed);
                Assert.Equal(PackageStatus.Available, fixture.Package.PackageStatusKey);
                Assert.Equal(PackageStatus.Available, other.PackageStatusKey);
                fixture.PackageService.Verify(x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), false), Times.Never);
            }

            [Fact]
            public async Task EntireBatchIsArchivedAndSavedBeforeAnyCleanup()
            {
                var fixture = new CaptureFixture();
                var other = CaptureFixture.NewPackage(2);

                await fixture.Service.SoftDeletePackagesAsync(
                    new[] { fixture.Package, other }, null, "reason", "admin-app", true);

                Assert.Equal(new[] { 1, 2 }, fixture.Saved.Select(b => b.PackageKey));
                Assert.Equal(4, fixture.Archived.Count);
                Assert.True(fixture.Events.LastIndexOf("save-archive") < fixture.Events.IndexOf("cleanup"));
                Assert.True(fixture.Events.LastIndexOf("save-json") < fixture.Events.IndexOf("cleanup"));
                fixture.Transaction.Verify(x => x.Commit(), Times.Once);
            }

            [Fact]
            public async Task SecondPackageArchiveFailureLeavesEntireBatchUntouched()
            {
                var fixture = new CaptureFixture();
                var other = CaptureFixture.NewPackage(2);
                var error = new IOException("archive unavailable");
                fixture.Files.Setup(x => x.StorePackageFileInBackupLocationAsync(other, It.IsAny<Stream>()))
                    .ThrowsAsync(error);

                Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => fixture.Service.SoftDeletePackagesAsync(
                    new[] { fixture.Package, other }, null, "reason", "admin-app", true)));

                fixture.VerifyNoCleanup();
                Assert.Equal(fixture.Package.Key, Assert.Single(fixture.Saved).PackageKey);
                Assert.Empty(fixture.Deletions);
                Assert.True(fixture.Package.Listed);
                Assert.True(other.Listed);
                Assert.Equal(PackageStatus.Available, fixture.Package.PackageStatusKey);
                Assert.Equal(PackageStatus.Available, other.PackageStatusKey);
                fixture.PackageService.Verify(x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), false), Times.Never);
                fixture.Backups.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public async Task MissingOrBlankExternalReadmeDoesNotBlockDeletion(string readme)
            {
                var fixture = new CaptureFixture();
                fixture.Files.Setup(x => x.DownloadReadMeMdFileAsync(fixture.Package)).ReturnsAsync(readme);

                await fixture.DeleteAsync();

                Assert.Equal(readme, Assert.Single(fixture.Saved).ExternalReadmeMarkdown);
                fixture.Files.Verify(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                fixture.PackageService.Verify(x => x.UpdatePackageStatusAsync(fixture.Package, PackageStatus.Deleted, false), Times.Once);
                Assert.Contains("commit", fixture.Events);
            }

            [Fact]
            public async Task MissingRequiredPackageFileBlocksCleanup()
            {
                var fixture = new CaptureFixture();
                fixture.Files.Setup(x => x.DownloadPackageFileAsync(fixture.Package)).ReturnsAsync((Stream)null);

                await Assert.ThrowsAsync<InvalidDataException>(() => fixture.DeleteAsync());

                fixture.VerifyNoCleanup();
            }

            [Fact]
            public async Task AlreadyDeletedPackageCannotReplaceCommittedBackup()
            {
                var fixture = new CaptureFixture();
                fixture.Package.PackageStatusKey = PackageStatus.Deleted;

                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DeleteAsync());

                Assert.Empty(fixture.Deletions);
                Assert.Empty(fixture.Saved);
                fixture.VerifyNoCleanup();
            }

            [Fact]
            public async Task IdentityFlushFailureDoesNotRemoveFilesOrCreateJson()
            {
                var fixture = new CaptureFixture();
                fixture.DeleteRepository.Setup(x => x.CommitChangesAsync())
                    .ThrowsAsync(new InvalidOperationException("identity SQL failure"));

                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DeleteAsync());

                fixture.VerifyNoCleanup();
                Assert.Empty(fixture.Saved);
                Assert.Empty(fixture.Deletions);
                fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            }

            [Fact]
            public async Task FileRemovalFailureRetainsRecoveryDataAndPropagatesOriginalError()
            {
                var fixture = new CaptureFixture();
                var error = new IOException("storage deletion failed");
                fixture.Files.Setup(x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(error);

                Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => fixture.DeleteAsync()));

                Assert.Single(fixture.Saved);
                fixture.VerifyArchivesCaptured();
                Assert.Empty(fixture.Deletions);
                Assert.Contains("cleanup", fixture.Events);
                Assert.DoesNotContain("commit", fixture.Events);
                fixture.Backups.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
                fixture.Transaction.Verify(x => x.Dispose(), Times.Once);
                fixture.PackageService.Verify(x => x.UpdatePackageStatusAsync(It.IsAny<Package>(), It.IsAny<PackageStatus>(), false), Times.Never);
            }

            [Fact]
            public async Task SqlFailureNeverAttemptsJsonDeletionAndPropagatesOriginalError()
            {
                var fixture = new CaptureFixture();
                var sqlError = new InvalidOperationException("SQL failure");
                fixture.PackageRepository.Setup(x => x.CommitChangesAsync()).ThrowsAsync(sqlError);

                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.DeleteAsync());

                Assert.Same(sqlError, error);
                Assert.Single(fixture.Saved);
                fixture.VerifyArchivesCaptured();
                Assert.Empty(fixture.Deletions);
                fixture.Backups.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            }

            [Fact]
            public async Task AmbiguousSymbolMappingBlocksCleanup()
            {
                var fixture = new CaptureFixture();
                fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 20, StatusKey = PackageStatus.Available, Hash = "different" });
                fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 21, StatusKey = PackageStatus.Available, Hash = "different" });
                fixture.SymbolFiles.Setup(x => x.DownloadPackageFileAsync(fixture.Package)).ReturnsAsync(() => new MemoryStream(new byte[] { 3 }));
                fixture.SymbolFiles.Setup(x => x.StorePackageFileInBackupLocationAsync(fixture.Package, It.IsAny<Stream>()))
                    .ReturnsAsync(CaptureFixture.PublicHash);

                await Assert.ThrowsAsync<InvalidDataException>(() => fixture.DeleteAsync());

                fixture.VerifyNoCleanup();
            }

            [Fact]
            public async Task EmbeddedReadmeDoesNotReadExternalMarkdown()
            {
                var fixture = new CaptureFixture();
                fixture.Package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;

                await fixture.DeleteAsync();

                Assert.Null(Assert.Single(fixture.Saved).ExternalReadmeMarkdown);
                fixture.Files.Verify(x => x.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Never);
            }

            private class CaptureFixture
            {
                public const string Readme = "# Original README\nUnicode: \u03bb, quotes: \"text\"";
                public static readonly string PublicHash = Hash(new byte[] { 1 });
                public static readonly string ValidationHash = Hash(new byte[] { 2 });
                public Package Package = NewPackage(1);
                public readonly List<PackageDelete> Deletions = new List<PackageDelete>();
                public readonly List<PackageDeleteBackup> Saved = new List<PackageDeleteBackup>();
                public readonly List<string> Archived = new List<string>();
                public readonly List<string> Events = new List<string>();
                public readonly Mock<IPackageFileService> Files = new Mock<IPackageFileService>();
                public readonly Mock<ISymbolPackageFileService> SymbolFiles = new Mock<ISymbolPackageFileService>();
                public readonly Mock<IPackageDeleteBackupService> Backups = new Mock<IPackageDeleteBackupService>();
                public readonly Mock<IEntityRepository<PackageDelete>> DeleteRepository = new Mock<IEntityRepository<PackageDelete>>();
                public readonly Mock<IEntityRepository<Package>> PackageRepository = new Mock<IEntityRepository<Package>>();
                public readonly Mock<IPackageService> PackageService = new Mock<IPackageService>();
                public readonly Mock<IAuditingService> Auditing = new Mock<IAuditingService>();
                public readonly Mock<IDbContextTransaction> Transaction = new Mock<IDbContextTransaction>();
                public readonly IPackageDeleteService Service;
                public Mock<IDatabase> Database;

                public CaptureFixture()
                {
                    var nextDeletionKey = 100;
                    DeleteRepository.Setup(x => x.InsertOnCommit(It.IsAny<PackageDelete>()))
                        .Callback<PackageDelete>(d => { d.Key = nextDeletionKey++; Deletions.Add(d); });
                    DeleteRepository.Setup(x => x.CommitChangesAsync())
                        .Callback(() => Events.Add("identity-flush")).Returns(Task.CompletedTask);
                    Files.Setup(x => x.DownloadPackageFileAsync(It.IsAny<Package>())).ReturnsAsync(() => new MemoryStream(new byte[] { 1 }));
                    Files.Setup(x => x.DownloadValidationPackageFileAsync(It.IsAny<Package>())).ReturnsAsync(() => new MemoryStream(new byte[] { 2 }));
                    Files.Setup(x => x.DownloadReadMeMdFileAsync(It.IsAny<Package>())).ReturnsAsync(Readme);
                    Files.Setup(x => x.StorePackageFileInBackupLocationAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                        .Returns<Package, Stream>((p, s) =>
                        {
                            var hash = CryptographyService.GenerateHash(s, CoreConstants.Sha512HashAlgorithmId);
                            Archived.Add(hash);
                            Events.Add("save-archive");
                            return Task.FromResult(hash);
                        });
                    Files.Setup(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                        .Callback(() => Events.Add("cleanup")).Returns(Task.CompletedTask);
                    var validator = new PackageDeleteBackupService(new Mock<ICoreFileStorageService>().Object);
                    Backups.Setup(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()))
                        .Returns<PackageDeleteBackup>(async b => { await validator.SaveAsync(b); Saved.Add(b); Events.Add("save-json"); });
                    var committed = false;
                    var deletionCount = 0;
                    Transaction.Setup(x => x.Commit()).Callback(() => { committed = true; Events.Add("commit"); });
                    Transaction.Setup(x => x.Dispose()).Callback(() =>
                    {
                        // Model rolled-back SQL rows only. Tracked entities and storage stay changed.
                        if (!committed)
                        {
                            Deletions.RemoveRange(deletionCount, Deletions.Count - deletionCount);
                        }
                    });
                    Service = CreateService(packageRepository: PackageRepository, packageDeletesRepository: DeleteRepository,
                        packageService: PackageService, packageFileService: Files, symbolPackageFileService: SymbolFiles,
                        packageDeleteBackupService: Backups, auditingService: Auditing,
                        configureDatabase: database =>
                        {
                            Database = database;
                            database.Setup(x => x.BeginTransaction()).Returns(() =>
                            {
                                committed = false;
                                deletionCount = Deletions.Count;
                                return Transaction.Object;
                            });
                        });
                }

                public Task DeleteAsync(bool admin = true)
                {
                    return admin
                        ? Service.SoftDeletePackagesAsync(new[] { Package }, null, "reason", "admin-app", true)
                        : Service.SoftDeletePackagesAsync(new[] { Package }, new User("owner"), "reason", "owner");
                }

                public void VerifyNoCleanup()
                {
                    Files.Verify(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    Files.Verify(x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    Files.Verify(x => x.DeleteReadMeMdFileAsync(It.IsAny<Package>()), Times.Never);
                    SymbolFiles.Verify(x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                    SymbolFiles.Verify(x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                }

                public void VerifyArchivesCaptured()
                {
                    Assert.Equal(new[] { PublicHash, ValidationHash }, Archived);
                }

                public static Package NewPackage(int key)
                {
                    return new Package
                    {
                        Key = key,
                        PackageRegistration = new PackageRegistration { Id = "Test.Package" },
                        Version = "1.0.0",
                        NormalizedVersion = "1.0.0",
                        PackageStatusKey = PackageStatus.Available,
                        Listed = true,
                        HasReadMe = true,
                        Hash = "database-hash-is-not-used"
                    };
                }

                private static string Hash(byte[] bytes)
                {
                    using (var stream = new MemoryStream(bytes))
                    {
                        return CryptographyService.GenerateHash(stream, CoreConstants.Sha512HashAlgorithmId);
                    }
                }
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

                Assert.Empty(packageRegistration.Packages);
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

                Assert.Empty(packageRegistration.Packages);
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
