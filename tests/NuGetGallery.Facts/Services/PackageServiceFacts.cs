// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Moq;
using NuGet;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class PackageServiceFacts
    {
        private static Mock<INupkg> CreateNuGetPackage(Action<Mock<IPackageMetadata>> setupMetadata = null)
        {
            var nugetPackage = new Mock<INupkg>();
            var metadata = new Mock<IPackageMetadata>();
            nugetPackage.Setup(x => x.Metadata).Returns(metadata.Object);

            metadata.Setup(x => x.Id).Returns("theId");
            metadata.Setup(x => x.Version).Returns(new SemanticVersion("01.0.42.0"));

            metadata.Setup(x => x.Authors).Returns(new[] { "theFirstAuthor", "theSecondAuthor" });
            metadata.Setup(x => x.DependencySets).Returns(
                new[]
                    {
                        new PackageDependencySet(
                            VersionUtility.DefaultTargetFramework,
                            new[]
                                {
                                    new NuGet.PackageDependency(
                                        "theFirstDependency",
                                        new VersionSpec
                                            {
                                                MinVersion = new SemanticVersion("1.0"),
                                                MaxVersion = new SemanticVersion("2.0"),
                                                IsMinInclusive = true,
                                                IsMaxInclusive = false
                                            }),
                                    new NuGet.PackageDependency("theSecondDependency", new VersionSpec(new SemanticVersion("1.0"))),
                                    new NuGet.PackageDependency("theThirdDependency")
                                }),
                        new PackageDependencySet(
                            VersionUtility.ParseFrameworkName("net35"),
                            new[]
                                {
                                    new NuGet.PackageDependency("theFourthDependency", new VersionSpec(new SemanticVersion("1.0")))
                                })
                    });
            metadata.Setup(x => x.Description).Returns("theDescription");
            metadata.Setup(x => x.ReleaseNotes).Returns("theReleaseNotes");
            metadata.Setup(x => x.IconUrl).Returns(new Uri("http://theiconurl/"));
            metadata.Setup(x => x.LicenseUrl).Returns(new Uri("http://thelicenseurl/"));
            metadata.Setup(x => x.ProjectUrl).Returns(new Uri("http://theprojecturl/"));
            metadata.Setup(x => x.RequireLicenseAcceptance).Returns(true);
            metadata.Setup(x => x.Summary).Returns("theSummary");
            metadata.Setup(x => x.Tags).Returns("theTags");
            metadata.Setup(x => x.Title).Returns("theTitle");
            metadata.Setup(x => x.Copyright).Returns("theCopyright");

            nugetPackage.Setup(x => x.GetStream()).Returns(new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }));

            if (setupMetadata != null)
            {
                setupMetadata(metadata);
            }

            return nugetPackage;
        }

        private static IPackageService CreateService(
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<Package>> packageRepository = null,
            Mock<IEntityRepository<PackageStatistics>> packageStatsRepo = null,
            Mock<IEntityRepository<PackageOwnerRequest>> packageOwnerRequestRepo = null,
            Mock<IIndexingService> indexingService = null,
            Action<Mock<PackageService>> setup = null)
        {
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            packageStatsRepo = packageStatsRepo ?? new Mock<IEntityRepository<PackageStatistics>>();
            packageOwnerRequestRepo = packageOwnerRequestRepo ?? new Mock<IEntityRepository<PackageOwnerRequest>>();
            indexingService = indexingService ?? new Mock<IIndexingService>();

            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                packageStatsRepo.Object,
                packageOwnerRequestRepo.Object,
                indexingService.Object);

            packageService.CallBase = true;

            if (setup != null)
            {
                setup(packageService);
            }

            return packageService.Object;
        }

        public class TheAddDownloadStatisticsMethod
        {
            [Fact]
            public void WillInsertNewRecordIntoTheStatisticsRepository()
            {
                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(
                    new PackageStatistics
                    {
                        Package = package,
                        IPAddress = "::1",
                        UserAgent = "Unit Test",
                        Operation = "Test Download",
                    });

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.Package == package && p.UserAgent == "Unit Test")));
                packageStatsRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillIgnoreTheIpAddressForNow()
            {
                // Until we understand privacy implications of storing IP Addresses thoroughly,
                // It's better to just not store them. Hence "unknown". - Phil Haack 10/6/2011

                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(new PackageStatistics
                {
                    Package = package,
                    IPAddress = "::1",
                    UserAgent = "Unit Test",
                });

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.IPAddress == "unknown")));
                packageStatsRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillAllowNullsForUserAgentAndUserHostAddress()
            {
                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(
                    new PackageStatistics
                    {
                        Package = package,
                    });

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.Package == package)));
                packageStatsRepo.Verify(x => x.CommitChanges());
            }
        }

        public class TheAddPackageOwnerMethod
        {
            [Fact]
            public void AddsUserToPackageOwnerCollection()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                packageRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRepository: packageRepository);

                service.AddPackageOwner(package, pendingOwner);

                Assert.Contains(pendingOwner, package.Owners);
                packageRepository.VerifyAll();
            }

            [Fact]
            public void RemovesRelatedPendingOwnerRequest()
            {
                var packageOwnerRequest = new PackageOwnerRequest { PackageRegistrationKey = 2, NewOwnerKey = 100, ConfirmationCode = "secret-token" };
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.DeleteOnCommit(packageOwnerRequest)).Verifiable();
                repository.Setup(r => r.CommitChanges()).Verifiable();
                repository.Setup(r => r.GetAll()).Returns(
                    new[]
                        {
                            new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token" },
                            packageOwnerRequest
                        }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                service.AddPackageOwner(package, pendingOwner);

                repository.VerifyAll();
            }
        }

        public class TheConfirmPackageOwnerMethod
        {
            [Fact]
            public void WithValidUserAndMatchingTokenReturnsSuccess()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                packageRepository.Setup(r => r.CommitChanges()).Verifiable();
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(
                    new[]
                        {
                            new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token" },
                            new PackageOwnerRequest { PackageRegistrationKey = 2, NewOwnerKey = 100, ConfirmationCode = "secret-token" }
                        }.AsQueryable());
                var service = CreateService(packageRepository: packageRepository, packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.Equal(ConfirmOwnershipResult.Success, result);
                Assert.Contains(pendingOwner, package.Owners);
                packageRepository.VerifyAll();
            }

            [Fact]
            public void WhenUserIsAlreadyOwnerReturnsAlreadyOwner()
            {
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var package = new PackageRegistration { Key = 2, Id = "pkg42", Owners = new[] { pendingOwner } };
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(
                    new[]
                        {
                            new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token" }
                        }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.Equal(ConfirmOwnershipResult.AlreadyOwner, result);
            }

            [Fact]
            public void WithNoMatchingPackgageOwnerRequestReturnsFailure()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(
                    new[]
                        {
                            new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token" }
                        }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.Equal(ConfirmOwnershipResult.Failure, result);
            }

            [Fact]
            public void WithValidUserAndNonMatchingTokenReturnsFailure()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                packageRepository.Setup(r => r.CommitChanges()).Throws(new InvalidOperationException());
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(
                    new[]
                        {
                            new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token" },
                            new PackageOwnerRequest { PackageRegistrationKey = 2, NewOwnerKey = 100, ConfirmationCode = "wrong-token" }
                        }.AsQueryable());
                var service = CreateService(packageRepository: packageRepository, packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.Equal(ConfirmOwnershipResult.Failure, result);
                Assert.DoesNotContain(pendingOwner, package.Owners);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionsForBadArguments()
            {
                var service = CreateService();

                Assert.Throws<ArgumentNullException>(() => service.ConfirmPackageOwner(null, new User(), "token"));
                Assert.Throws<ArgumentNullException>(() => service.ConfirmPackageOwner(new PackageRegistration(), null, "token"));
                Assert.Throws<ArgumentNullException>(() => service.ConfirmPackageOwner(new PackageRegistration(), null, null));
                Assert.Throws<ArgumentNullException>(() => service.ConfirmPackageOwner(new PackageRegistration(), null, ""));
            }
        }

        public class TheCreatePackageMethod
        {
            [Fact]
            public void WillCreateANewPackageRegistrationUsingTheNugetPackIdWhenOneDoesNotAlreadyExist()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                    {
                        mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });

                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                service.CreatePackage(nugetPackage.Object, currentUser);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Id == "theId")));
                packageRegistrationRepository.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillMakeTheCurrentUserTheOwnerWhenCreatingANewPackageRegistration()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                service.CreatePackage(nugetPackage.Object, currentUser);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Owners.Contains(currentUser))));
            }

            [Fact]
            public void WillReadThePropertiesFromTheNuGetPackageWhenCreatingANewPackage()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                // Yes, I know this is a lot of asserts. Yes, I know I broke the golden, one assert per test rule.
                // That said, it's still asserting one "thing": that the package data was read.
                // I'm sorry, but I just can't imagine adding a test per property.
                // Note that there is no assertion on package identifier, because that's at the package registration level (and covered in another test).
                Assert.Equal("01.0.42.0", package.Version);
                Assert.Equal("1.0.42", package.NormalizedVersion);
                Assert.Equal("theFirstDependency", package.Dependencies.ElementAt(0).Id);
                Assert.Equal("[1.0, 2.0)", package.Dependencies.ElementAt(0).VersionSpec);
                Assert.Equal("theSecondDependency", package.Dependencies.ElementAt(1).Id);
                Assert.Equal("[1.0]", package.Dependencies.ElementAt(1).VersionSpec);
                Assert.Equal("theDescription", package.Description);
                Assert.Equal("theReleaseNotes", package.ReleaseNotes);
                Assert.Equal("http://theiconurl/", package.IconUrl);
                Assert.Equal("http://thelicenseurl/", package.LicenseUrl);
                Assert.Equal("http://theprojecturl/", package.ProjectUrl);
                Assert.Equal(true, package.RequiresLicenseAcceptance);
                Assert.Equal("theSummary", package.Summary);
                Assert.Equal("theTags", package.Tags);
                Assert.Equal("theTitle", package.Title);
                Assert.Equal("theCopyright", package.Copyright);
                Assert.Null(package.Language);
                Assert.False(package.IsPrerelease);

                Assert.Equal("theFirstAuthor, theSecondAuthor", package.FlattenedAuthors);
                Assert.Equal(
                    "theFirstDependency:[1.0, 2.0):net4000|theSecondDependency:[1.0]:net4000|theThirdDependency::net4000|theFourthDependency:[1.0]:net35",
                    package.FlattenedDependencies);
            }

            [Fact]
            public void WillReadTheLanguagePropertyFromThePackage()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage(m => m.Setup(s => s.Language).Returns("fr"));
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                // Assert
                Assert.Equal("fr", package.Language);
            }

            [Fact]
            public void WillReadPrereleaseFlagFromNuGetPackage()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Returns(1).Verifiable();
                packageRegistrationRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Version).Returns(new SemanticVersion("2.14.0-a")));
                var currentUser = new User();

                // Act
                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                // Assert
                Assert.True(package.IsPrerelease);
                packageRegistrationRepository.Verify();
            }

            [Fact]
            public void DoNotCommitChangesIfCommitChangesIsFalse()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Returns(1).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Version).Returns(new SemanticVersion("2.14.0-a")));
                var currentUser = new User();

                // Act
                var package = service.CreatePackage(nugetPackage.Object, currentUser, commitChanges: false);

                // Assert
                packageRegistrationRepository.Verify();
            }

            [Fact]
            public void DoNotUpdateIndexIfCommitChangesIsFalse()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Returns(1).Verifiable();
                var indexingService = new Mock<IIndexingService>(MockBehavior.Strict);
                var service = CreateService(indexingService: indexingService, packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });

                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Version).Returns(new SemanticVersion("2.14.0-a")));
                var currentUser = new User();

                // Act
                var package = service.CreatePackage(nugetPackage.Object, currentUser, commitChanges: false);
            }

            [Fact]
            public void UpdateIndexIfCommitChangesIsTrue()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Returns(1).Verifiable();
                packageRegistrationRepository.Setup(r => r.CommitChanges()).Verifiable();
                var indexingService = new Mock<IIndexingService>(MockBehavior.Strict);
                indexingService.Setup(s => s.UpdateIndex()).Verifiable();
                var service = CreateService(indexingService: indexingService, packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Version).Returns(new SemanticVersion("2.14.0-a")));
                var currentUser = new User();

                // Act
                var package = service.CreatePackage(nugetPackage.Object, currentUser, commitChanges: true);

                // Assert
                indexingService.Verify();
            }

            [Fact]
            public void CommitChangesIfCommitChangesIsTrue()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Returns(1).Verifiable();
                packageRegistrationRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Version).Returns(new SemanticVersion("2.14.0-a")));
                var currentUser = new User();

                // Act
                var package = service.CreatePackage(nugetPackage.Object, currentUser, commitChanges: true);

                // Assert
                packageRegistrationRepository.Verify();
            }

            [Fact]
            public void WillGenerateAHashForTheCreatedPackage()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                var expectedHash = CryptographyService.GenerateHash(CreateNuGetPackage().Object.GetStream().ReadAllBytes(), Constants.Sha512HashAlgorithmId);
                Assert.Equal(expectedHash, package.Hash);
                Assert.Equal(Constants.Sha512HashAlgorithmId, package.HashAlgorithm);
            }

            [Fact]
            public void WillNotCreateThePackageInAnUnpublishedState()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                Assert.NotNull(package.Published);
            }

            [Fact]
            public void WillSetTheNewPackagesCreatedAndLastUpdatedTimes()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                Assert.NotEqual(DateTime.MinValue, package.Created);
                Assert.NotEqual(DateTime.MinValue, package.LastUpdated);
            }

            [Fact]
            public void WillSaveThePackageFileAndSetThePackageFileSize()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                Assert.Equal(8, package.PackageFileSize);
            }

            [Fact]
            private void WillSaveTheCreatedPackageWhenANewPackageRegistrationIsCreated()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Packages.ElementAt(0) == package)));
                packageRegistrationRepository.Verify(x => x.CommitChanges());
            }

            [Fact]
            private void WillSaveTheCreatedPackageWhenThePackageRegistrationAlreadyExisted()
            {
                var currentUser = new User();
                var packageRegistration = new PackageRegistration
                    {
                        Id = "theId",
                        Owners = new HashSet<User> { currentUser },
                    };
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration); });
                var nugetPackage = CreateNuGetPackage();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                Assert.Same(packageRegistration.Packages.ElementAt(0), package);
                packageRegistrationRepository.Verify(x => x.CommitChanges());
            }

            [Fact]
            private void WillThrowIfThePackageRegistrationAlreadyExistsAndTheCurrentUserIsNotAnOwner()
            {
                var currentUser = new User();
                var packageRegistration = new PackageRegistration
                    {
                        Id = "theId",
                        Owners = new HashSet<User>()
                    };
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration); });
                var nugetPackage = CreateNuGetPackage();

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, currentUser, true));

                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageIdIsLongerThan128()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Metadata.Id).Returns("theId".PadRight(129, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Id", "128"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageAuthorsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage =
                    CreateNuGetPackage(m => m.Setup(x => x.Authors)
                                             .Returns(new[]
                                             {
                                                 "theFirstAuthor".PadRight(2001, '_'),
                                                 "theSecondAuthor".PadRight(2001, '_')
                                             }));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Authors", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageCopyrightIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage =
                    CreateNuGetPackage(m => m.Setup(x => x.Copyright).Returns("theCopyright".PadRight(4001, '_')));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheVersionIsLongerThan64Characters()
            {
                var service = CreateService();
                var versionString = "1.0.0-".PadRight(65, 'a');
                var nugetPackage =
                    CreateNuGetPackage(m => m.Setup(x => x.Version).Returns(SemanticVersion.Parse(versionString)));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Version", "64"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageDependenciesIsLongerThanInt16MaxValue()
            {
                var service = CreateService();
                var versionSpec = VersionUtility.ParseVersionSpec("[1.0]");
                var dependencySets = new[]
                {
                    new PackageDependencySet(
                        VersionUtility.DefaultTargetFramework,
                        Enumerable.Repeat(
                            new NuGet.PackageDependency("theFirstDependency", versionSpec),
                            5000))
                };
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.DependencySets).Returns(dependencySets));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependencies", Int16.MaxValue), ex.Message);
            }

            [Fact]
            private void WillThrowIfThPackageDependencyIdIsLongerThan128()
            {
                var service = CreateService();
                var dependencySets = new[]
                {
                    new PackageDependencySet(VersionUtility.DefaultTargetFramework, new NuGet.PackageDependency[0]),
                    new PackageDependencySet(
                        new FrameworkName(".NetFramework", new Version(4, 0)),
                        new[]
                        {
                            new NuGet.PackageDependency(
                            "theFirstDependency".PadRight(129, '_'),
                            new VersionSpec
                            {
                                MinVersion = new SemanticVersion("1.0"),
                                MaxVersion = new SemanticVersion("2.0"),
                                IsMinInclusive = true,
                                IsMaxInclusive = false
                            })
                        })
                };
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.DependencySets).Returns(dependencySets));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependency.Id", 128), ex.Message);
            }

            [Fact]
            private void WillThrowIfThPackageDependencyVersionSpecIsLongerThan256()
            {
                var service = CreateService();
                var dependencySets = new[]
                {
                    new PackageDependencySet(VersionUtility.DefaultTargetFramework, new NuGet.PackageDependency[0]),
                    new PackageDependencySet(
                        new FrameworkName(".NetFramework", new Version(4, 0)),
                        new[]
                        {
                            new NuGet.PackageDependency(
                            "theFirstDependency",
                            new VersionSpec
                            {
                                MinVersion = new SemanticVersion("1.0-".PadRight(257, 'a')),
                                MaxVersion = new SemanticVersion("2.0"),
                                IsMinInclusive = true,
                                IsMaxInclusive = false
                            })
                        })
                };
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.DependencySets).Returns(dependencySets));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependency.VersionSpec", 256), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageDescriptionIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Description).Returns("theDescription".PadRight(4001, '_')));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Description", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageIconUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.IconUrl).Returns(new Uri("http://theIconUrl/".PadRight(4001, '-'), UriKind.Absolute)));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageLicenseUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.LicenseUrl).Returns(new Uri("http://theLicenseUrl/".PadRight(4001, '-'), UriKind.Absolute)));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageProjectUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.ProjectUrl).Returns(new Uri("http://theProjectUrl/".PadRight(4001, '-'), UriKind.Absolute)));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageSummaryIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Summary).Returns("theSummary".PadRight(4001, '_')));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Summary", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageTagsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Tags).Returns("theTags".PadRight(4001, '_')));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Tags", "4000"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageTitleIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Title).Returns("theTitle".PadRight(4001, '_')));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Title", "256"), ex.Message);
            }

            [Fact]
            private void WillThrowIfTheNuGetPackageLanguageIsLongerThan20()
            {
                // Arrange
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage(m => m.Setup(x => x.Language).Returns(new string('a', 21)));

                // Act
                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                // Assert
                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Language", "20"), ex.Message);
            }

            [Fact]
            private void WillSaveSupportedFrameworks()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                               {
                                   mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                                   mockPackageService.Setup(p => p.GetSupportedFrameworks(It.IsAny<INupkg>())).Returns(
                                       new[]
                                           {
                                               VersionUtility.ParseFrameworkName("net40"),
                                               VersionUtility.ParseFrameworkName("net35")
                                           });
                               });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                Assert.Equal("net40", package.SupportedFrameworks.First().TargetFramework);
                Assert.Equal("net35", package.SupportedFrameworks.ElementAt(1).TargetFramework);
            }

            [Fact]
            private void WillNotSaveAnySuuportedFrameworksWhenThereIsANullTargetFramework()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                               {
                                   mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                                   mockPackageService.Setup(p => p.GetSupportedFrameworks(It.IsAny<INupkg>())).Returns(
                                       new[]
                                           {
                                               null,
                                               VersionUtility.ParseFrameworkName("net35")
                                           });
                               });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(nugetPackage.Object, currentUser);

                Assert.Empty(package.SupportedFrameworks);
            }
        }

        public class TheCreatePackageOwnerRequestMethod
        {
            [Fact]
            public void CreatesPackageOwnerRequest()
            {
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                var service = CreateService(packageOwnerRequestRepo: packageOwnerRequestRepository);
                var package = new PackageRegistration { Key = 1 };
                var owner = new User { Key = 100 };
                var newOwner = new User { Key = 200 };

                service.CreatePackageOwnerRequest(package, owner, newOwner);

                packageOwnerRequestRepository.Verify(
                    r => r.InsertOnCommit(
                        It.Is<PackageOwnerRequest>(req => req.PackageRegistrationKey == 1 && req.RequestingOwnerKey == 100 && req.NewOwnerKey == 200))
                    );
            }

            [Fact]
            public void ReturnsExistingMatchingPackageOwnerRequest()
            {
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                packageOwnerRequestRepository.Setup(r => r.GetAll()).Returns(
                    new[]
                        {
                            new PackageOwnerRequest
                                {
                                    PackageRegistrationKey = 1,
                                    RequestingOwnerKey = 99,
                                    NewOwnerKey = 200
                                }
                        }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: packageOwnerRequestRepository);
                var package = new PackageRegistration { Key = 1 };
                var owner = new User { Key = 100 };
                var newOwner = new User { Key = 200 };

                var request = service.CreatePackageOwnerRequest(package, owner, newOwner);

                Assert.Equal(99, request.RequestingOwnerKey);
            }
        }

        public class TheDeletePackageMethod
        {
            [Fact]
            public void DoNotCommitIfCommitChangesIsFalse()
            {
                // Arrange
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();

                var service = CreateService(packageRepository: packageRepository, setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                // Act
                service.DeletePackage("hot", "1.0", commitChanges: false);

                // Assert
                packageRepository.Verify(r => r.CommitChanges(), Times.Never());
            }

            [Fact]
            public void CommitIfCommitChangesIsTrue()
            {
                // Arrange
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();

                var service = CreateService(packageRepository: packageRepository, setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                // Act
                service.DeletePackage("hot", "1.0", commitChanges: true);

                // Assert
                packageRepository.Verify(r => r.CommitChanges(), Times.Once());
            }

            [Fact]
            public void WillDeleteThePackage()
            {
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.DeletePackage("theId", "1.0.42");

                packageRepository.Verify(x => x.DeleteOnCommit(package));
                packageRepository.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillDeleteThePackageRegistrationIfThereAreNoOtherPackages()
            {
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0" };
                packageRegistration.Packages.Add(package);
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepository.Setup(r => r.DeleteOnCommit(package)).Callback(() => { packageRegistration.Packages.Remove(package); });
                packageRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, packageRepository: packageRepository, setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.DeletePackage("theId", "1.0.42");

                packageRegistrationRepository.Verify(x => x.DeleteOnCommit(packageRegistration));
            }

            [Fact]
            public void WillNotDeleteThePackageRegistrationIfThereAreOtherPackages()
            {
                var packageRegistration = new PackageRegistration { Packages = new HashSet<Package>() };
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0" };
                packageRegistration.Packages.Add(package);
                packageRegistration.Packages.Add(new Package { Version = "0.9" });
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var packageRepository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepository.Setup(r => r.DeleteOnCommit(package)).Callback(() => { packageRegistration.Packages.Remove(package); });
                packageRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, packageRepository: packageRepository, setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.DeletePackage("theId", "1.0.42");

                packageRegistrationRepository.Verify(x => x.DeleteOnCommit(packageRegistration), Times.Never());
            }

            [Fact]
            public void WillUpdateIsLatest()
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
                packageRepository.Setup(r => r.DeleteOnCommit(package100)).Callback(() => { packages.Remove(package100); }).Verifiable();
                packageRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package100); });

                // Act
                service.DeletePackage("A", "1.0.0");

                // Assert
                Assert.True(package10A.IsLatest);
                Assert.False(package10A.IsLatestStable);
                Assert.False(package09.IsLatest);
                Assert.True(package09.IsLatestStable);
                packageRepository.Verify();
            }

            [Fact]
            public void WillThrowIfThePackageDoesNotExist()
            {
                var service = CreateService(setup:
                        mockService => { mockService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), false)).Returns((Package)null); });

                var ex = Assert.Throws<EntityException>(() => service.DeletePackage("theId", "1.0.42"));

                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }
        }

        public class TheFindPackageByIdAndVersionMethod
        {
            [Fact]
            public void ReturnsTheRequestedPackageVersion()
            {
                var service = CreateService(setup:
                    mockPackageService =>
                    {
                        mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Throws(
                            new Exception("This should not be called when the version is specified."));
                    });

                service.FindPackageByIdAndVersion("theId", "1.0.42");

                // Nothing to assert because it's too complicated to test the actual LINQ expression.
                // What we're testing via the throw above is that it didn't load the registration and get the latest version.
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void WillThrowIfIdIsNullOrEmpty(string id)
            {
                var service = CreateService();
                var ex = Assert.Throws<ArgumentNullException>(() => service.FindPackageByIdAndVersion(id, "1.0.42"));
                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public void ReturnsTheLatestStableVersionIfAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("theId", version: null);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0", result.Version);
            }

            [Fact]
            public void ReturnsTheLatestVersionIfNoLatestStableVersionIsAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0.0b", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("theId", version: null);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0.0b", result.Version);
            }

            [Fact]
            public void ReturnsNullIfNoLatestStableVersionIsAvailableAndPrereleaseIsDisallowed()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0.0b", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("theId", version: null, allowPrerelease: false);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ReturnsTheMostRecentVersionIfNoLatestVersionIsAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0.0b", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = false };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = false };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("theId", null);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0.0b", result.Version);
            }
        }

        public class TheFindPackagesByOwnerMethod : TestContainer
        {
            [Fact]
            public void ReturnsAListedPackage()
            {
                var owner = new User { Username = "someone" };
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { owner } };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatest = true, IsLatestStable = true };
                packageRegistration.Packages.Add(package);

                var context = GetFakeContext();
                context.Users.Add(owner);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package);
                var service = Get<PackageService>();

                var packages = service.FindPackagesByOwner(owner, includeUnlisted: false);
                Assert.Equal(1, packages.Count());
            }

            [Fact]
            public void ReturnsNoUnlistedPackagesWhenIncludeUnlistedIsFalse()
            {
                var owner = new User { Username = "someone" };
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { owner } };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false, IsLatest = false, IsLatestStable = false };
                packageRegistration.Packages.Add(package);

                var context = GetFakeContext();
                context.Users.Add(owner);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package);
                var service = Get<PackageService>();

                var packages = service.FindPackagesByOwner(owner, includeUnlisted: false);
                Assert.Equal(0, packages.Count());
            }

            [Fact]
            public void ReturnsAnUnlistedPackageWhenIncludeUnlistedIsTrue()
            {
                var owner = new User { Username = "someone" };
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { owner } };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false, IsLatest = false, IsLatestStable = false };
                packageRegistration.Packages.Add(package);

                var context = GetFakeContext();
                context.Users.Add(owner);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package);
                var service = Get<PackageService>();

                var packages = service.FindPackagesByOwner(owner, includeUnlisted: true);
                Assert.Equal(1, packages.Count());
            }

            [Fact]
            public void ReturnsAPackageForEachPackageRegistration()
            {
                var owner = new User { Username = "someone" };
                var packageRegistrationA = new PackageRegistration { Id = "idA", Owners = { owner } };
                var packageRegistrationB = new PackageRegistration { Id = "idB", Owners = { owner } };
                var packageA = new Package { Version = "1.0", PackageRegistration = packageRegistrationA, Listed = true, IsLatest = true, IsLatestStable = true };
                var packageB = new Package { Version = "1.0", PackageRegistration = packageRegistrationB, Listed = true, IsLatest = true, IsLatestStable = true };
                packageRegistrationA.Packages.Add(packageA);
                packageRegistrationB.Packages.Add(packageB);

                var context = GetFakeContext();
                context.Users.Add(owner);
                context.PackageRegistrations.Add(packageRegistrationA);
                context.PackageRegistrations.Add(packageRegistrationB);
                context.Packages.Add(packageA);
                context.Packages.Add(packageB);
                var service = Get<PackageService>();

                var packages = service.FindPackagesByOwner(owner, includeUnlisted: false).ToList();
                Assert.Equal(2, packages.Count);
                Assert.Contains(packageA, packages);
                Assert.Contains(packageB, packages);
            }

            [Fact]
            public void ReturnsOnlyLatestStablePackageIfBothExist()
            {
                var owner = new User { Username = "someone" };
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { owner } };
                var latestPackage = new Package { Version = "2.0.0-alpha", PackageRegistration = packageRegistration, Listed = true, IsLatest = true };
                var latestStablePackage = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                packageRegistration.Packages.Add(latestPackage);
                packageRegistration.Packages.Add(latestStablePackage);

                var context = GetFakeContext();
                context.Users.Add(owner);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(latestPackage);
                context.Packages.Add(latestStablePackage);
                var service = Get<PackageService>();

                var packages = service.FindPackagesByOwner(owner, includeUnlisted: false).ToList();
                Assert.Equal(1, packages.Count);
                Assert.Contains(latestStablePackage, packages);
            }
        }

        public class TheMarkPackageListedMethod
        {
            [Fact]
            public void SetsListedToTrue()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.MarkPackageListed(package);

                Assert.True(package.Listed);
            }

            [Fact]
            public void DoNotCommitIfCommitChangesIsFalse()
            {
                // Assert
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                service.MarkPackageListed(package, commitChanges: false);

                // Assert
                packageRepository.Verify(p => p.CommitChanges(), Times.Never());
            }

            [Fact]
            public void CommitIfCommitChangesIsTrue()
            {
                // Assert
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                service.MarkPackageListed(package, commitChanges: true);

                // Assert
                packageRepository.Verify(p => p.CommitChanges(), Times.Once());
            }

            [Fact]
            public void OnPackageVersionHigherThanLatestSetsItToLatestVersion()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var packages = new[]
                    {
                        new Package
                            {
                                Version = "1.0.1",
                                PackageRegistration = packageRegistration,
                                Listed = false,
                                IsLatest = false,
                                IsLatestStable = false
                            },
                        new Package
                            {
                                Version = "1.0.0",
                                PackageRegistration = packageRegistration,
                                Listed = true,
                                IsLatest = true,
                                IsLatestStable = true
                            }
                    }.ToList();
                packageRegistration.Packages = packages;
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.MarkPackageListed(packages[0]);

                Assert.True(packageRegistration.Packages.ElementAt(0).IsLatest);
                Assert.True(packageRegistration.Packages.ElementAt(0).IsLatestStable);
                Assert.False(packages.ElementAt(1).IsLatest);
                Assert.False(packages.ElementAt(1).IsLatestStable);
            }
        }

        public class TheMarkPackageUnlistedMethod
        {
            [Fact]
            public void SetsListedToFalse()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.MarkPackageUnlisted(package);

                Assert.False(package.Listed);
            }

            [Fact]
            public void CommitIfCommitChangesIfTrue()
            {
                // Act
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                service.MarkPackageUnlisted(package, commitChanges: true);

                // Assert
                packageRepository.Verify(p => p.CommitChanges(), Times.Once());
            }

            [Fact]
            public void DoNotCommitIfCommitChangesIfFalse()
            {
                // Act
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                service.MarkPackageUnlisted(package, commitChanges: false);

                // Assert
                packageRepository.Verify(p => p.CommitChanges(), Times.Never());
            }

            [Fact]
            public void OnLatestPackageVersionSetsPreviousToLatestVersion()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var packages = new[]
                    {
                        new Package
                            { Version = "1.0.1", PackageRegistration = packageRegistration, IsLatest = true, IsLatestStable = true },
                        new Package
                            { Version = "1.0.0", PackageRegistration = packageRegistration, IsLatest = false, IsLatestStable = false }
                    }.ToList();
                packageRegistration.Packages = packages;
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.MarkPackageUnlisted(packages[0]);

                Assert.False(packageRegistration.Packages.ElementAt(0).IsLatest);
                Assert.False(packageRegistration.Packages.ElementAt(0).IsLatestStable);
                Assert.True(packages.ElementAt(1).IsLatest);
                Assert.True(packages.ElementAt(1).IsLatestStable);
            }

            [Fact]
            public void OnOnlyListedPackageSetsNoPackageToLatestVersion()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0.1", PackageRegistration = packageRegistration, IsLatest = true, IsLatestStable = true };
                packageRegistration.Packages = new List<Package>(new[] { package });
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.MarkPackageUnlisted(package);

                Assert.False(package.IsLatest, "IsLatest");
                Assert.False(package.IsLatestStable, "IsLatestStable");
            }
        }

        public class ThePublishPackageMethod
        {
            [Fact]
            public void WillSetThePublishedDateOnThePackageBeingPublished()
            {
                var package = new Package
                    {
                        Version = "1.0.42",
                        PackageRegistration = new PackageRegistration
                            {
                                Id = "theId",
                                Packages = new HashSet<Package>()
                            }
                    };
                package.PackageRegistration.Packages.Add(package);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage("theId", "1.0.42");

                Assert.NotNull(package.Published);
                packageRepository.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillSetThePublishedDateOnThePackageBeingPublishedWithOverload()
            {
                var package = new Package
                {
                    Version = "1.0.42",
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage(package, commitChanges: false);

                Assert.NotNull(package.Published);
                packageRepository.Verify(x => x.CommitChanges(), Times.Never());
            }

            [Fact]
            public void WillSetUpdateIsLatestStableOnThePackageWhenItIsTheLatestVersionWithOverload()
            {
                var package = new Package
                    {
                        Version = "1.0.42",
                        PackageRegistration = new PackageRegistration
                            {
                                Id = "theId",
                                Packages = new HashSet<Package>()
                            }
                    };

                package.PackageRegistration.Packages.Add(package);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage(package);

                Assert.True(package.IsLatestStable);
            }

            [Fact]
            public void WillSetUpdateIsLatestStableOnThePackageWhenItIsTheLatestVersion()
            {
                var package = new Package
                {
                    Version = "1.0.42",
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                package.PackageRegistration.Packages.Add(new Package { Version = "1.0", PackageRegistration = package.PackageRegistration });
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage("theId", "1.0.42");

                Assert.True(package.IsLatestStable);
            }

            [Fact]
            public void WillNotSetUpdateIsLatestStableOnThePackageWhenItIsNotTheLatestVersionWithOverload()
            {
                var package = new Package
                    {
                        Version = "1.0.42",
                        PackageRegistration = new PackageRegistration
                            {
                                Id = "theId",
                                Packages = new HashSet<Package>()
                            }
                    };
                package.PackageRegistration.Packages.Add(package);
                package.PackageRegistration.Packages.Add(
                    new Package
                        {
                            Version = "2.0",
                            PackageRegistration = package.PackageRegistration,
                            Published = DateTime.UtcNow
                        });
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage(package);

                Assert.False(package.IsLatestStable);
            }

            [Fact]
            public void PublishPackageUpdatesIsAbsoluteLatestForPrereleasePackage()
            {
                var package = new Package
                    {
                        Version = "1.0.42-alpha",
                        Published = DateTime.Now,
                        PackageRegistration = new PackageRegistration
                            {
                                Id = "theId",
                                Packages = new HashSet<Package>()
                            },
                        IsPrerelease = true,
                    };
                package.PackageRegistration.Packages.Add(package);
                var package39 = new Package
                    {
                        Version = "1.0.39",
                        PackageRegistration = package.PackageRegistration,
                        Published = DateTime.Now.AddDays(-1)
                    };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage("theId", "1.0.42-alpha");
                Assert.True(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public void PublishPackageUpdatesIsAbsoluteLatestForPrereleasePackageWithOverload()
            {
                var package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.Now,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    },
                    IsPrerelease = true,
                };
                package.PackageRegistration.Packages.Add(package);
                var package39 = new Package
                {
                    Version = "1.0.39",
                    PackageRegistration = package.PackageRegistration,
                    Published = DateTime.Now.AddDays(-1)
                };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage(package);

                Assert.True(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public void SetUpdateDoesNotSetIsLatestStableForAnyIfAllPackagesArePrerelease()
            {
                var package = new Package
                    {
                        Version = "1.0.42-alpha",
                        Published = DateTime.Now,
                        IsPrerelease = true,
                        PackageRegistration = new PackageRegistration
                            {
                                Id = "theId",
                                Packages = new HashSet<Package>()
                            }
                    };
                package.PackageRegistration.Packages.Add(package);
                var package39 = new Package
                    {
                        Version = "1.0.39-beta",
                        PackageRegistration = package.PackageRegistration,
                        Published = DateTime.Now.AddDays(-1),
                        IsPrerelease = true
                    };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage("theId", "1.0.42-alpha");
                Assert.False(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public void SetUpdateDoesNotSetIsLatestStableForAnyIfAllPackagesArePrereleaseWithOverload()
            {
                var package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.Now,
                    IsPrerelease = true,
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                var package39 = new Package
                {
                    Version = "1.0.39-beta",
                    PackageRegistration = package.PackageRegistration,
                    Published = DateTime.Now.AddDays(-1),
                    IsPrerelease = true
                };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package); });

                service.PublishPackage(package);
                Assert.False(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public void WillThrowIfThePackageDoesNotExist()
            {
                var service = CreateService(setup:
                        mockPackageService =>
                        {
                            mockPackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(
                                (Package)null);
                        });

                var ex = Assert.Throws<EntityException>(() => service.PublishPackage("theId", "1.0.42"));

                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }

            [Fact]
            public void WillThrowIfThePackageIsNull()
            {
                var service = CreateService();

                Assert.Throws<ArgumentNullException>(() => service.PublishPackage(null));
            }
        }

        public class TheRemovePackageOwnerMethod
        {
            [Fact]
            public void RemovesPackageOwner()
            {
                var service = CreateService();
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Owners = new List<User> { owner1, owner2 } };

                service.RemovePackageOwner(package, owner1);

                Assert.DoesNotContain(owner1, package.Owners);
            }

            [Fact]
            public void WontRemoveLastOwner()
            {
                var service = CreateService();
                var singleOwner = new User { Key = 1, Username = "Owner" };
                var package = new PackageRegistration { Owners = new List<User> { singleOwner } };

                Assert.Throws<InvalidOperationException>(
                    () => service.RemovePackageOwner(package, singleOwner));
            }

            [Fact]
            public void RemovesPendingPackageOwner()
            {
                var packageOwnerRequest = new PackageOwnerRequest
                    {
                        PackageRegistrationKey = 1,
                        RequestingOwnerKey = 99,
                        NewOwnerKey = 200
                    };
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                packageOwnerRequestRepository.Setup(r => r.GetAll()).Returns(new[] { packageOwnerRequest }.AsQueryable());
                packageOwnerRequestRepository.Setup(r => r.DeleteOnCommit(packageOwnerRequest)).Verifiable();
                packageOwnerRequestRepository.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageOwnerRequestRepo: packageOwnerRequestRepository);
                var pendingOwner = new User { Key = 200 };
                var owner = new User();
                var package = new PackageRegistration { Key = 1, Owners = new List<User> { owner } };

                service.RemovePackageOwner(package, pendingOwner);

                Assert.Contains(owner, package.Owners);
                packageOwnerRequestRepository.VerifyAll();
            }
        }

        public class TheSetLicenseReportVisibilityMethod
        {
            [Fact]
            public void SetsHideLicenseReportFalseWhenVisibleTrue()
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    HideLicenseReport = true
                };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.SetLicenseReportVisibility(package, true);

                Assert.False(package.HideLicenseReport);
            }

            [Fact]
            public void SetsHideLicenseReportTrueWhenVisibleFalse()
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    HideLicenseReport = false
                };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                service.SetLicenseReportVisibility(package, false);

                Assert.True(package.HideLicenseReport);
            }
        }
    }
}
