// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class CorePackageServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void Constructor_WhenPackageRepositoryIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new CorePackageService(
                        packageRepository: null,
                        packageRegistrationRepository: Mock.Of<IEntityRepository<PackageRegistration>>(),
                        certificateRepository: Mock.Of<IEntityRepository<Certificate>>()));

                Assert.Equal("packageRepository", exception.ParamName);
            }

            [Fact]
            public void Constructor_WhenPackageRegistrationRepositoryIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new CorePackageService(
                        Mock.Of<IEntityRepository<Package>>(),
                        packageRegistrationRepository: null,
                        certificateRepository: Mock.Of<IEntityRepository<Certificate>>()));

                Assert.Equal("packageRegistrationRepository", exception.ParamName);
            }

            [Fact]
            public void Constructor_WhenCertificateRepositoryIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new CorePackageService(
                        Mock.Of<IEntityRepository<Package>>(),
                        Mock.Of<IEntityRepository<PackageRegistration>>(),
                        certificateRepository: null));

                Assert.Equal("certificateRepository", exception.ParamName);
            }
        }

        public class TheUpdatePackageStreamMetadataMethod
        {
            [Fact]
            public async Task RejectsNullPackage()
            {
                // Arrange
                Package package = null;
                var metadata = new PackageStreamMetadata();
                var service = CreateService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.UpdatePackageStreamMetadataAsync(package, metadata, commitChanges: true));
            }

            [Fact]
            public async Task RejectsNullStreamMetadata()
            {
                // Arrange
                var package = new Package();
                PackageStreamMetadata metadata = null;
                var service = CreateService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.UpdatePackageStreamMetadataAsync(package, metadata, commitChanges: true));
            }

            [Theory]
            [InlineData(false, 0)]
            [InlineData(true, 1)]
            public async Task CommitsTheCorrectNumberOfTimes(bool commitChanges, int commitCount)
            {
                // Arrange
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var package = new Package();
                var metadata = new PackageStreamMetadata();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdatePackageStreamMetadataAsync(package, metadata, commitChanges);

                // Assert
                packageRepository.Verify(x => x.CommitChangesAsync(), Times.Exactly(commitCount));
            }

            [Fact]
            public async Task UpdatesTheStreamMetadata()
            {
                // Arrange
                var package = new Package
                {
                    Hash = "hash-before",
                    HashAlgorithm = "hash-algorithm-before",
                    PackageFileSize = 23,
                    LastUpdated = new DateTime(2017, 1, 1, 8, 30, 0),
                    LastEdited = new DateTime(2017, 1, 1, 7, 30, 0),
                    PackageStatusKey = PackageStatus.Available,
                };
                var metadata = new PackageStreamMetadata
                {
                    Hash = "hash-after",
                    HashAlgorithm = "hash-algorithm-after",
                    Size = 42,
                };
                var service = CreateService();

                // Act
                var before = DateTime.UtcNow;
                await service.UpdatePackageStreamMetadataAsync(package, metadata, commitChanges: true);
                var after = DateTime.UtcNow;

                // Assert
                Assert.Equal("hash-after", package.Hash);
                Assert.Equal("hash-algorithm-after", package.HashAlgorithm);
                Assert.Equal(42, package.PackageFileSize);
                Assert.InRange(package.LastUpdated, before, after);
                Assert.NotNull(package.LastEdited);
                Assert.InRange(package.LastEdited.Value, before, after);
                Assert.Equal(package.LastUpdated, package.LastEdited);
            }

            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.Validating)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task DoesNotUpdateLastEditedWhenNotAvailable(PackageStatus packageStatus)
            {
                // Arrange
                var originalLastEdited = new DateTime(2017, 1, 1, 7, 30, 0);
                var package = new Package
                {
                    LastEdited = originalLastEdited,
                    PackageStatusKey = packageStatus,
                };
                var metadata = new PackageStreamMetadata();
                var service = CreateService();

                // Act
                await service.UpdatePackageStreamMetadataAsync(package, metadata, commitChanges: true);

                // Assert
                Assert.Equal(originalLastEdited, package.LastEdited);
            }
        }

        public class TheUpdatePackageStatusMethod
        {
            [Fact]
            public async Task RejectsNullPackage()
            {
                // Arrange
                Package package = null;
                var service = CreateService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.UpdatePackageStatusAsync(package, PackageStatus.Deleted, commitChanges: true));
            }

            [Fact]
            public async Task RejectsNullPackageRegistration()
            {
                // Arrange
                Package package = new Package
                {
                    PackageRegistration = null,
                };
                var service = CreateService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.UpdatePackageStatusAsync(package, PackageStatus.Deleted, commitChanges: true));
            }

            [Theory]
            [InlineData(false, 0)]
            [InlineData(true, 1)]
            public async Task CommitsTheCorrectNumberOfTimes(bool commitChanges, int commitCount)
            {
                // Arrange
                var packageRepositoy = new Mock<IEntityRepository<Package>>();
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration(),
                };
                var service = CreateService(packageRepository: packageRepositoy);

                // Act
                await service.UpdatePackageStatusAsync(package, PackageStatus.Available, commitChanges);

                // Assert
                packageRepositoy.Verify(x => x.CommitChangesAsync(), Times.Exactly(commitCount));
            }

            [Fact]
            public async Task SetDeletedBitWhenNewPackageStatusIsAvailable()
            {
                // Arrange
                var service = CreateService();
                var package = new Package
                {
                    PackageStatusKey = PackageStatus.Available,
#pragma warning disable CS0612 // Type or member is obsolete
                    Deleted = false,
#pragma warning restore CS0612 // Type or member is obsolete
                    PackageRegistration = new PackageRegistration
                    {
                        Packages = new List<Package>(),
                    }
                };
                package.PackageRegistration.Packages.Add(package);

                // Act
                await service.UpdatePackageStatusAsync(package, PackageStatus.Deleted);

                // Assert
#pragma warning disable CS0612 // Type or member is obsolete
                Assert.True(package.Deleted);
#pragma warning restore CS0612 // Type or member is obsolete
            }

            [Fact]
            public async Task UpdateCorrectPropertiesWhenBecomingAvailable()
            {
                // Arrange
                var service = CreateService();
                var package = new Package
                {
                    Version = "1.0.0",
                    PackageStatusKey = PackageStatus.Deleted,
#pragma warning disable CS0612 // Type or member is obsolete
                    Deleted = true,
#pragma warning restore CS0612 // Type or member is obsolete
                    PackageRegistration = new PackageRegistration
                    {
                        Packages = new List<Package>(),
                    },
                    IsLatest = false,
                    IsLatestStable = false,
                    IsLatestSemVer2 = false,
                    IsLatestStableSemVer2 = false,
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                    LastEdited = DateTime.MinValue,
                    LastUpdated = DateTime.MinValue,
                };
                package.PackageRegistration.Packages.Add(package);

                // Act
                var before = DateTime.UtcNow;
                await service.UpdatePackageStatusAsync(package, PackageStatus.Available);
                var after = DateTime.UtcNow;

                // Assert
#pragma warning disable CS0612 // Type or member is obsolete
                Assert.False(package.Deleted, $"{nameof(Package.Deleted)} should be false.");
#pragma warning restore CS0612 // Type or member is obsolete
                Assert.Equal(PackageStatus.Available, package.PackageStatusKey);
                Assert.True(package.IsLatest, $"{nameof(Package.IsLatest)} should be true.");
                Assert.True(package.IsLatestStable, $"{nameof(Package.IsLatestStable)} should be true.");
                Assert.True(package.IsLatestSemVer2, $"{nameof(Package.IsLatestSemVer2)} should be true.");
                Assert.True(package.IsLatestStableSemVer2, $"{nameof(Package.IsLatestStableSemVer2)} should be true.");
                Assert.NotNull(package.LastEdited);
                Assert.InRange(package.LastEdited.Value, before, after);
                Assert.InRange(package.LastUpdated, before, after);
            }

            [Fact]
            public async Task DoesNotUpdateLatestPropertiesWhenNoLatestAndNotBecomingAvailable()
            {
                // Arrange
                var service = CreateService();
                var package = new Package
                {
                    PackageStatusKey = PackageStatus.Available,
#pragma warning disable CS0612 // Type or member is obsolete
                    Deleted = false,
#pragma warning restore CS0612 // Type or member is obsolete
                    PackageRegistration = new PackageRegistration
                    {
                        Packages = new List<Package>(),
                    },
                    IsLatest = false,
                    IsLatestStable = false,
                    IsLatestSemVer2 = false,
                    IsLatestStableSemVer2 = false,
                    SemVerLevelKey = SemVerLevelKey.Unknown,
                };
                package.PackageRegistration.Packages.Add(package);

                // Act
                var before = DateTime.UtcNow;
                await service.UpdatePackageStatusAsync(package, PackageStatus.Deleted);
                var after = DateTime.UtcNow;

                // Assert
                Assert.False(package.IsLatest, $"{nameof(Package.IsLatest)} should be false.");
                Assert.False(package.IsLatestStable, $"{nameof(Package.IsLatestStable)} should be false.");
                Assert.False(package.IsLatestSemVer2, $"{nameof(Package.IsLatestSemVer2)} should be false.");
                Assert.False(package.IsLatestStableSemVer2, $"{nameof(Package.IsLatestStableSemVer2)} should be false.");
                Assert.Null(package.LastEdited);
                Assert.InRange(package.LastUpdated, before, after);
            }

            [Fact]
            public async Task DoesNotUpdateLastEditedWhenBecomingUnavailable()
            {
                // Arrange
                var service = CreateService();
                var package = new Package
                {
                    PackageStatusKey = PackageStatus.Available,
                    PackageRegistration = new PackageRegistration
                    {
                        Packages = new List<Package>(),
                    },
                };
                package.PackageRegistration.Packages.Add(package);

                // Act
                await service.UpdatePackageStatusAsync(package, PackageStatus.Deleted);

                // Assert
                Assert.Null(package.LastEdited);
            }
        }

        public class TheUpdateIsLatestMethod
        {
            [Fact]
            public async Task DoNotCommitIfCommitChangesIsFalse()
            {
                // Arrange
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packageRegistration.Packages.Add(package);
                var packageRepository = new Mock<IEntityRepository<Package>>();

                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, commitChanges: false);

                // Assert
                packageRepository.Verify(r => r.CommitChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task CommitIfCommitChangesIsTrue()
            {
                // Arrange
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packageRegistration.Packages.Add(package);
                var packageRepository = new Mock<IEntityRepository<Package>>();

                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, true);

                // Assert
                packageRepository.Verify(r => r.CommitChangesAsync(), Times.Once());
            }

            [Fact]
            public async Task UpdatesPackageRegistrationIdIfDifferentFromLatestSemVer1()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "nugeT.ioT" };
                var latestSemVer1 = new Package { PackageRegistration = packageRegistration, Id = "nuget.iot", Version = "1.0.0" };
                packageRegistration.Packages.Add(latestSemVer1);

                var service = CreateService();

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, commitChanges: false);

                // Assert
                Assert.Equal("nuget.iot", packageRegistration.Id);
            }

            [Fact]
            public async Task UpdatesPackageRegistrationIdIfDifferentFromLatestSemVer2()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "nugeT.ioT" };
                var latestSemVer1 = new Package { PackageRegistration = packageRegistration, Id = "nuget.iot", Version = "1.0.0" };
                packageRegistration.Packages.Add(latestSemVer1);
                var latestSemVer2 = new Package { PackageRegistration = packageRegistration, Id = "NuGet.IoT", Version = "2.0.0-beta.1" };
                packageRegistration.Packages.Add(latestSemVer2);

                var service = CreateService();

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, commitChanges: false);

                // Assert
                Assert.Equal("NuGet.IoT", packageRegistration.Id);
            }

            [Fact]
            public async Task DoesNotUpdatePackageRegistrationIdIfLatestHasNoVersionSpecificId()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "nugeT.ioT" };
                var latestSemVer1 = new Package { PackageRegistration = packageRegistration, Id = "nuget.iot", Version = "1.0.0" };
                packageRegistration.Packages.Add(latestSemVer1);
                var latestSemVer2 = new Package { PackageRegistration = packageRegistration, Version = "2.0.0-beta.1" };
                packageRegistration.Packages.Add(latestSemVer2);

                var service = CreateService();

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, commitChanges: false);

                // Assert
                Assert.Equal("nugeT.ioT", packageRegistration.Id);
            }

            [Fact]
            public async Task DoesNotUpdatePackageRegistrationIdIfNoLatest()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "nugeT.ioT" };
                var latestSemVer1 = new Package { PackageRegistration = packageRegistration, Id = "nuget.iot", Listed = false, Version = "1.0.0" };
                packageRegistration.Packages.Add(latestSemVer1);

                var service = CreateService();

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, commitChanges: false);

                // Assert
                Assert.Equal("nugeT.ioT", packageRegistration.Id);
            }

            [Fact]
            public async Task ResetsCurrentLatestPackageVersionsBeforeUpdate()
            {
                // Arrange
                var packageRegistration = new PackageRegistration();

                var previousLatestStable = new Package { PackageRegistration = packageRegistration, Version = "1.0.0", IsLatestStable = true };
                packageRegistration.Packages.Add(previousLatestStable);
                var previousLatest = new Package { PackageRegistration = packageRegistration, Version = "1.0.0-alpha", IsLatest = true, IsPrerelease = true };
                packageRegistration.Packages.Add(previousLatest);
                var previousLatestStableSemVer2 = new Package { PackageRegistration = packageRegistration, Version = "1.0.1+metadata", IsLatestStableSemVer2 = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };
                packageRegistration.Packages.Add(previousLatestStableSemVer2);
                var previousLatestSemVer2 = new Package { PackageRegistration = packageRegistration, Version = "1.0.1-alpha.1", IsLatestSemVer2 = true, IsPrerelease = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };
                packageRegistration.Packages.Add(previousLatestSemVer2);

                // Simulates adding newer versions, to ensure the previous latest are no longer latest at end of test.
                var newLatestStable = new Package { PackageRegistration = packageRegistration, Version = "1.0.1", IsLatestStable = true };
                packageRegistration.Packages.Add(newLatestStable);
                var newLatest = new Package { PackageRegistration = packageRegistration, Version = "1.0.2-alpha", IsLatest = true, IsPrerelease = true };
                packageRegistration.Packages.Add(newLatest);
                var newLatestStableSemVer2 = new Package { PackageRegistration = packageRegistration, Version = "1.0.2+metadata", IsLatestStableSemVer2 = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };
                packageRegistration.Packages.Add(newLatestStableSemVer2);
                var newLatestSemVer2 = new Package { PackageRegistration = packageRegistration, Version = "1.0.3-alpha.1", IsLatestSemVer2 = true, IsPrerelease = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };
                packageRegistration.Packages.Add(newLatestSemVer2);

                var packageRepository = new Mock<IEntityRepository<Package>>();

                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, commitChanges: true);

                // Assert
                Assert.False(previousLatestStable.IsLatestStable);
                Assert.False(previousLatest.IsLatest);
                Assert.False(previousLatestSemVer2.IsLatestSemVer2);
                Assert.False(previousLatestStableSemVer2.IsLatestStableSemVer2);

                Assert.True(newLatestStable.IsLatestStable);
                Assert.True(newLatest.IsLatest);
                Assert.True(newLatestSemVer2.IsLatestSemVer2);
                Assert.True(newLatestStableSemVer2.IsLatestStableSemVer2);
            }

            [Fact]
            public async Task UpdateIsLatestScenarioForPrereleaseAsAbsoluteLatest()
            {
                // Arrange
                var packages = new HashSet<Package>();
                var packageRegistration = new PackageRegistration { Packages = packages };
                var package10A = new Package { PackageRegistration = packageRegistration, Version = "1.0.0-a", IsPrerelease = true };
                packages.Add(package10A);
                var package09 = new Package { PackageRegistration = packageRegistration, Version = "0.9.0" };
                packages.Add(package09);
                var packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepository.Setup(r => r.CommitChangesAsync())
                    .Returns(Task.FromResult(0)).Verifiable();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, true);

                // Assert
                Assert.True(package10A.IsLatest);
                Assert.True(package10A.IsLatestSemVer2);
                Assert.False(package10A.IsLatestStable);
                Assert.False(package10A.IsLatestStableSemVer2);

                Assert.False(package09.IsLatest);
                Assert.False(package09.IsLatestSemVer2);
                Assert.True(package09.IsLatestStable);
                Assert.True(package09.IsLatestStableSemVer2);

                packageRepository.Verify();
            }

            [Fact]
            public async Task UpdateIsLatestScenarioForStableAsAbsoluteLatest()
            {
                // Arrange
                var packages = new HashSet<Package>();
                var packageRegistration = new PackageRegistration { Packages = packages };
                var package100 = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packages.Add(package100);
                var package10A = new Package { PackageRegistration = packageRegistration, Version = "1.0.0-a", IsPrerelease = true };
                packages.Add(package10A);
                var package09 = new Package { PackageRegistration = packageRegistration, Version = "0.9.0" };
                packages.Add(package09);
                var packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepository.Setup(r => r.CommitChangesAsync())
                    .Returns(Task.FromResult(0)).Verifiable();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, true);

                // Assert
                Assert.True(package100.IsLatest);
                Assert.True(package100.IsLatestSemVer2);
                Assert.True(package100.IsLatestStable);
                Assert.True(package100.IsLatestStableSemVer2);

                Assert.False(package10A.IsLatest);
                Assert.False(package10A.IsLatestSemVer2);
                Assert.False(package10A.IsLatestStable);
                Assert.False(package10A.IsLatestStableSemVer2);

                Assert.False(package09.IsLatest);
                Assert.False(package09.IsLatestSemVer2);
                Assert.False(package09.IsLatestStable);
                Assert.False(package09.IsLatestStableSemVer2);

                packageRepository.Verify();
            }

            [Fact]
            public async Task UpdateIsLatestScenarioForSemVer2PrereleaseAsAbsoluteLatest()
            {
                // Arrange
                var packages = new HashSet<Package>();
                var packageRegistration = new PackageRegistration { Packages = packages };
                var semVer2Package = new Package { PackageRegistration = packageRegistration, Version = "1.0.1-alpha.1", IsPrerelease = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };
                packages.Add(semVer2Package);
                var package100 = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packages.Add(package100);
                var package10A = new Package { PackageRegistration = packageRegistration, Version = "1.0.0-a", IsPrerelease = true };
                packages.Add(package10A);
                var package09 = new Package { PackageRegistration = packageRegistration, Version = "0.9.0" };
                packages.Add(package09);
                var packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepository.Setup(r => r.CommitChangesAsync())
                    .Returns(Task.FromResult(0)).Verifiable();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, true);

                // Assert
                Assert.True(semVer2Package.IsLatestSemVer2);
                Assert.False(semVer2Package.IsLatestStableSemVer2);
                Assert.False(semVer2Package.IsLatest);
                Assert.False(semVer2Package.IsLatestStable);

                Assert.True(package100.IsLatest);
                Assert.False(package100.IsLatestSemVer2);
                Assert.True(package100.IsLatestStable);
                Assert.True(package100.IsLatestStableSemVer2);

                Assert.False(package10A.IsLatest);
                Assert.False(package10A.IsLatestSemVer2);
                Assert.False(package10A.IsLatestStable);
                Assert.False(package10A.IsLatestStableSemVer2);

                Assert.False(package09.IsLatest);
                Assert.False(package09.IsLatestSemVer2);
                Assert.False(package09.IsLatestStable);
                Assert.False(package09.IsLatestStableSemVer2);

                packageRepository.Verify();
            }

            [Fact]
            public async Task UpdateIsLatestScenarioForSemVer2StableAsAbsoluteLatest()
            {
                // Arrange
                var packages = new HashSet<Package>();
                var packageRegistration = new PackageRegistration { Packages = packages };
                var semVer2Package = new Package { PackageRegistration = packageRegistration, Version = "1.0.1+metadata", SemVerLevelKey = SemVerLevelKey.SemVer2 };
                packages.Add(semVer2Package);
                var package100 = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packages.Add(package100);
                var package10A = new Package { PackageRegistration = packageRegistration, Version = "1.0.0-a", IsPrerelease = true };
                packages.Add(package10A);
                var package09 = new Package { PackageRegistration = packageRegistration, Version = "0.9.0" };
                packages.Add(package09);
                var packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepository.Setup(r => r.CommitChangesAsync())
                    .Returns(Task.FromResult(0)).Verifiable();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.UpdateIsLatestAsync(packageRegistration, true);

                // Assert
                Assert.True(semVer2Package.IsLatestSemVer2);
                Assert.True(semVer2Package.IsLatestStableSemVer2);
                Assert.False(semVer2Package.IsLatest);
                Assert.False(semVer2Package.IsLatestStable);

                Assert.True(package100.IsLatest);
                Assert.False(package100.IsLatestSemVer2);
                Assert.True(package100.IsLatestStable);
                Assert.False(package100.IsLatestStableSemVer2);

                Assert.False(package10A.IsLatest);
                Assert.False(package10A.IsLatestSemVer2);
                Assert.False(package10A.IsLatestStable);
                Assert.False(package10A.IsLatestStableSemVer2);

                Assert.False(package09.IsLatest);
                Assert.False(package09.IsLatestSemVer2);
                Assert.False(package09.IsLatestStable);
                Assert.False(package09.IsLatestStableSemVer2);

                packageRepository.Verify();
            }
        }

        public class TheFindPackageRegistrationByIdMethod
        {
            private readonly Mock<CorePackageService> _service;

            public TheFindPackageRegistrationByIdMethod()
            {
                _service = CreateMockService();
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void FindPackageRegistrationById_WhenPackageIdIsInvalid_Throws(string packageId)
            {
                var exception = Assert.Throws<ArgumentException>(
                    () => _service.Object.FindPackageRegistrationById(packageId));

                Assert.Equal("packageId", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public void FindPackageRegistrationById_WhenPackageIdNotFound_ReturnsNull()
            {
                var packageRegistration = _service.Object.FindPackageRegistrationById(packageId: "nonexistant");

                Assert.Null(packageRegistration);
            }

            [Fact]
            public void FindPackageRegistrationById_WhenPackageIdFound_ReturnsPackageRegistration()
            {
                var packageRegistration = new PackageRegistration()
                {
                    Key = 1,
                    Id = "a",
                };
                var package = new Package()
                {
                    PackageRegistration = packageRegistration
                };

                packageRegistration.Packages.Add(package);

                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();

                packageRegistrationRepository.Setup(x => x.GetAll())
                    .Returns(new EnumerableQuery<PackageRegistration>(new[] { packageRegistration }));

                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository);

                var actualResult = service.FindPackageRegistrationById(packageRegistration.Id);

                Assert.Same(packageRegistration, actualResult);
            }
        }

        public class TheUpdatePackageSigningCertificateAsyncMethod
        {
            private readonly Package _package;
            private readonly PackageRegistration _packageRegistration;
            private readonly Certificate _certificate;
            private readonly Mock<CorePackageService> _service;
            private readonly Mock<IEntityRepository<Certificate>> _certificateRepository;
            private readonly Mock<IEntityRepository<Package>> _packageRepository;

            public TheUpdatePackageSigningCertificateAsyncMethod()
            {
                _packageRegistration = new PackageRegistration()
                {
                    Key = 2,
                    Id = "b"
                };
                _package = new Package()
                {
                    Key = 3,
                    PackageRegistration = _packageRegistration,
                    NormalizedVersion = "1.2.3"
                };
                _certificate = new Certificate()
                {
                    Key = 4,
                    Thumbprint = "c"
                };

                _packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                _certificateRepository = new Mock<IEntityRepository<Certificate>>(MockBehavior.Strict);

                _service = CreateMockService(
                    packageRepository: _packageRepository,
                    certificateRepository: _certificateRepository);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task UpdatePackageSigningCertificateAsync_WhenPackageIdIsInvalid_Throws(string packageId)
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.Object.UpdatePackageSigningCertificateAsync(
                        packageId,
                        packageVersion: "1.0.0",
                        thumbprint: "a"));

                Assert.Equal("packageId", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task UpdatePackageSigningCertificateAsync_WhenPackageVersionIsInvalid_Throws(string packageVersion)
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.Object.UpdatePackageSigningCertificateAsync(
                        packageId: "a",
                        packageVersion: packageVersion,
                        thumbprint: "a"));

                Assert.Equal("packageVersion", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public async Task UpdatePackageSigningCertificateAsync_WhenThumbprintIsInvalid_Throws(string thumbprint)
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.Object.UpdatePackageSigningCertificateAsync(
                        packageId: "a",
                        packageVersion: "1.0.0",
                        thumbprint: thumbprint));

                Assert.Equal("thumbprint", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public async Task UpdatePackageSigningCertificateAsync_WhenPackageIsNotFound_Throws()
            {
                _service.Setup(x => x.FindPackageByIdAndVersionStrict(
                        It.Is<string>(id => id == _packageRegistration.Id),
                        It.Is<string>(version => version == _package.NormalizedVersion)))
                    .Returns<Package>(null);

                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.Object.UpdatePackageSigningCertificateAsync(
                        _packageRegistration.Id,
                        _package.NormalizedVersion,
                        _certificate.Thumbprint));

                Assert.StartsWith("The package does not exist.", exception.Message);

                VerifyMockExpectations();
            }

            [Fact]
            public async Task UpdatePackageSigningCertificateAsync_WhenCertificateIsNotFound_Throws()
            {
                _service.Setup(x => x.FindPackageByIdAndVersionStrict(
                        It.Is<string>(id => id == _packageRegistration.Id),
                        It.Is<string>(version => version == _package.NormalizedVersion)))
                    .Returns(_package);

                _certificateRepository.Setup(x => x.GetAll())
                    .Returns(new EnumerableQuery<Certificate>(Enumerable.Empty<Certificate>()));

                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => _service.Object.UpdatePackageSigningCertificateAsync(
                        _packageRegistration.Id,
                        _package.NormalizedVersion,
                        _certificate.Thumbprint));

                Assert.StartsWith("The certificate does not exist.", exception.Message);

                VerifyMockExpectations();
            }

            [Fact]
            public async Task UpdatePackageSigningCertificateAsync_WhenPackageDoesNotNeedUpdating_Succeeds()
            {
                _service.Setup(x => x.FindPackageByIdAndVersionStrict(
                        It.Is<string>(id => id == _packageRegistration.Id),
                        It.Is<string>(version => version == _package.NormalizedVersion)))
                    .Returns(_package);
                _certificateRepository.Setup(x => x.GetAll())
                    .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));

                _package.CertificateKey = _certificate.Key;

                await _service.Object.UpdatePackageSigningCertificateAsync(
                    _packageRegistration.Id,
                    _package.NormalizedVersion,
                    _certificate.Thumbprint);

                VerifyMockExpectations();
            }

            [Fact]
            public async Task UpdatePackageSigningCertificateAsync_WhenPackageNeedsUpdating_Succeeds()
            {
                _service.Setup(x => x.FindPackageByIdAndVersionStrict(
                        It.Is<string>(id => id == _packageRegistration.Id),
                        It.Is<string>(version => version == _package.NormalizedVersion)))
                    .Returns(_package);
                _certificateRepository.Setup(x => x.GetAll())
                    .Returns(new EnumerableQuery<Certificate>(new[] { _certificate }));
                _packageRepository.Setup(x => x.CommitChangesAsync())
                    .Returns(Task.Delay(0));

                await _service.Object.UpdatePackageSigningCertificateAsync(
                    _packageRegistration.Id,
                    _package.NormalizedVersion,
                    _certificate.Thumbprint);

                VerifyMockExpectations();
            }

            private void VerifyMockExpectations()
            {
                _packageRepository.VerifyAll();
                _certificateRepository.VerifyAll();
                _service.VerifyAll();
            }
        }

        private static ICorePackageService CreateService(
            Mock<IEntityRepository<Package>> packageRepository = null,
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<Certificate>> certificateRepository = null)
        {
            return CreateMockService(packageRepository, packageRegistrationRepository, certificateRepository).Object;
        }

        private static Mock<CorePackageService> CreateMockService(
            Mock<IEntityRepository<Package>> packageRepository = null,
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<Certificate>> certificateRepository = null)
        {
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            certificateRepository = certificateRepository ?? new Mock<IEntityRepository<Certificate>>();

            var packageService = new Mock<CorePackageService>(
                packageRepository.Object,
                packageRegistrationRepository.Object,
                certificateRepository.Object);

            packageService.CallBase = true;

            return packageService;
        }
    }
}