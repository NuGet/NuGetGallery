// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
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
            Action<Mock<TestPackageDeleteService>> setup = null,
            bool useRealConstructor = false)
        {
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageDeletesRepository = packageDeletesRepository ?? new Mock<IEntityRepository<PackageDelete>>();

            var dbContext = new Mock<DbContext>();
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            entitiesContext.Setup(m => m.GetDatabase()).Returns(new DatabaseWrapper(dbContext.Object.Database));

            packageService = packageService ?? new Mock<IPackageService>();
            indexingService = indexingService ?? new Mock<IIndexingService>();
            packageFileService = packageFileService ?? new Mock<IPackageFileService>();

            auditingService = auditingService ?? new Mock<IAuditingService>();

            config = config ?? new Mock<IPackageDeleteConfiguration>();

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
                    config.Object);
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
                    config.Object);

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
                IPackageDeleteConfiguration config) : base(
                    packageRepository,
                    packageRegistrationRepository,
                    packageDeletesRepository,
                    entitiesContext,
                    packageService,
                    indexingService,
                    packageFileService,
                    auditingService,
                    config)
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
            public async Task WillUpdateThePackageRepository()
            {
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var packageDeleteRepository = new Mock<IEntityRepository<PackageDelete>>();
                var service = CreateService(packageRepository: packageRepository, packageDeletesRepository: packageDeleteRepository);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", Hash = _packageHashForTests };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.SoftDeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);
                
                packageRepository.Verify(x => x.CommitChangesAsync());
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

                var service = CreateService(packageRepository: packageRepository, packageRegistrationRepository: packageRegistrationRepository, auditingService: auditingService);
                
                if (succeeds)
                {
                    await service.ReflowHardDeletedPackageAsync(id, version);
                }
                else
                {
                    await Assert.ThrowsAsync<UserSafeException>(() => service.ReflowHardDeletedPackageAsync(id, version));
                }
                
                auditingService.Verify(x => x.SaveAuditRecordAsync(It.IsAny<AuditRecord>()), succeeds ? Times.Once() : Times.Never());
            }
        }
    }
}
