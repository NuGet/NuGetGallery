// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class PackageServiceFacts
    {
        private static IPackageService CreateService(
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<Package>> packageRepository = null,
            IPackageNamingConflictValidator packageNamingConflictValidator = null,
            IAuditingService auditingService = null,
            Mock<ITelemetryService> telemetryService = null,
            Action<Mock<PackageService>> setup = null)
        {
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            auditingService = auditingService ?? new TestAuditingService();
            telemetryService = telemetryService ?? new Mock<ITelemetryService>();

            if (packageNamingConflictValidator == null)
            {
                packageNamingConflictValidator = new PackageNamingConflictValidator(
                    packageRegistrationRepository.Object,
                    packageRepository.Object);
            }

            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                packageNamingConflictValidator,
                auditingService,
                telemetryService.Object);

            packageService.CallBase = true;

            if (setup != null)
            {
                setup(packageService);
            }

            return packageService.Object;
        }

        public class TheAddPackageOwnerMethod
        {
            [Fact]
            public async Task AddsUserToPackageOwnerCollection()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                packageRepository.Setup(r => r.CommitChangesAsync())
                    .Returns(Task.CompletedTask).Verifiable();
                var service = CreateService(packageRepository: packageRepository);

                await service.AddPackageOwnerAsync(package, pendingOwner);

                Assert.Contains(pendingOwner, package.Owners);
                packageRepository.VerifyAll();
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

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Id == "theId")));
            }

            [Fact]
            public async Task WillThrowWhenCreateANewPackageRegistrationWithAnIdThatMatchesAnExistingPackageTitle()
            {
                // Arrange
                var idThatMatchesExistingTitle = "AwesomePackage";

                var currentUser = new User();
                var existingPackageRegistration = new PackageRegistration
                {
                    Id = "SomePackageId",
                    Owners = new HashSet<User>()
                };
                var existingPackage = new Package
                {
                    PackageRegistration = existingPackageRegistration,
                    Version = "1.0.0",
                    Title = idThatMatchesExistingTitle
                };

                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                packageRegistrationRepository.Setup(r => r.GetAll()).Returns(new[] { existingPackageRegistration }.AsQueryable());

                var packageRepository = new Mock<IEntityRepository<Package>>();
                packageRepository.Setup(r => r.GetAll()).Returns(new[] { existingPackage }.AsQueryable());

                var service = CreateService(
                    packageRegistrationRepository: packageRegistrationRepository,
                    packageRepository: packageRepository,
                    setup: mockPackageService =>
                    {
                        mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });

                // Act
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: idThatMatchesExistingTitle);

                // Assert
                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false));

                Assert.Equal(String.Format(Strings.NewRegistrationIdMatchesExistingPackageTitle, idThatMatchesExistingTitle), ex.Message);
            }

            [Fact]
            public void WillMakeTheCurrentUserTheOwnerWhenCreatingANewPackageRegistration()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Owners.Contains(currentUser))));
            }

            [Fact]
            public async Task WillReadThePropertiesFromTheNuGetPackageWhenCreatingANewPackage()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                // Note that there is no assertion on package identifier, because that's at the package registration level (and covered in another test).
                Assert.Equal("01.0.42.0", package.Version);
                Assert.Equal("1.0.42", package.NormalizedVersion);
                Assert.Equal("theFirstDependency", package.Dependencies.ElementAt(0).Id);
                Assert.Equal("[1.0.0, 2.0.0)", package.Dependencies.ElementAt(0).VersionSpec);
                Assert.Equal("theSecondDependency", package.Dependencies.ElementAt(1).Id);
                Assert.Equal("[1.0.0, 1.0.0]", package.Dependencies.ElementAt(1).VersionSpec);
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
                    "theFirstDependency:[1.0.0, 2.0.0):net40|theSecondDependency:[1.0.0, 1.0.0]:net40|theThirdDependency:(, ):net40|theFourthDependency:[1.0.0, 1.0.0]:net35",
                    package.FlattenedDependencies);
            }

            [Fact]
            public async Task WillReadTheLanguagePropertyFromThePackage()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(language: "fr");
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                // Assert
                Assert.Equal("fr", package.Language);
            }

            [Fact]
            public async Task WillReadPrereleaseFlagFromNuGetPackage()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.GetAll()).Returns(Enumerable.Empty<PackageRegistration>().AsQueryable());
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(version: "2.14.0-a");
                var currentUser = new User();

                // Act
                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                // Assert
                Assert.True(package.IsPrerelease);
                packageRegistrationRepository.Verify();
            }

            [Fact]
            public async Task DoNotCommitChanges()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepository.Setup(r => r.GetAll()).Returns(Enumerable.Empty<PackageRegistration>().AsQueryable());
                packageRegistrationRepository.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Verifiable();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(version: "2.14.0-a");
                var currentUser = new User();

                // Act
                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                // Assert
                packageRegistrationRepository.Verify();
                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WillStoreTheHashForTheCreatedPackage()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var packageStream = nugetPackage.Object.GetStream().AsSeekableStream();
                var expectedHash = CryptographyService.GenerateHash(packageStream, CoreConstants.Sha512HashAlgorithmId);
                var packageStreamMetadata = new PackageStreamMetadata
                {
                    Hash = expectedHash,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Size = packageStream.Length
                };
                var package = await service.CreatePackageAsync(nugetPackage.Object, packageStreamMetadata, currentUser, currentUser, isVerified: false);

                Assert.Equal(expectedHash, package.Hash);
                Assert.Equal(CoreConstants.Sha512HashAlgorithmId, package.HashAlgorithm);
            }

            [Fact]
            public async Task WillNotCreateThePackageInAnUnpublishedState()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.NotNull(package.Published);
            }

            [Fact]
            public async Task WillNotSetTheNewPackagesCreatedAndLastUpdatedTimesAsTheDatabaseShouldDoIt()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal(DateTime.MinValue, package.Created);
            }

            [Fact]
            public async Task WillSetTheNewPackagesLastUpdatedTimes()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.NotEqual(DateTime.MinValue, package.LastUpdated);
            }

            [Fact]
            public async Task WillSaveThePackageFileAndSetThePackageFileSize()
            {
                var service = CreateService(setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var packageStream = nugetPackage.Object.GetStream().AsSeekableStream();
                var packageHash = CryptographyService.GenerateHash(packageStream, CoreConstants.Sha512HashAlgorithmId);
                var packageStreamMetadata = new PackageStreamMetadata
                {
                    Hash = packageHash,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    Size = 618
                };
                var package = await service.CreatePackageAsync(nugetPackage.Object, packageStreamMetadata, currentUser, currentUser, isVerified: false);

                Assert.Equal(618, package.PackageFileSize);
            }

            [Fact]
            private async Task WillSaveTheCreatedPackageWhenANewPackageRegistrationIsCreated()
            {
                var key = 0;
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var owner = new User { Key = key++, Username = "owner" };
                var currentUser = new User { Key = key++, Username = "currentUser" };

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner, currentUser, isVerified: false);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Packages.ElementAt(0) == package)));
                Assert.True(package.PackageRegistration.Owners.SequenceEqual(new[] { owner }));
                Assert.Equal(currentUser, package.User);
            }

            [Fact]
            private async Task WillSaveTheCreatedPackageWhenThePackageRegistrationAlreadyExisted()
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
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Same(packageRegistration.Packages.ElementAt(0), package);
            }

            [Theory]
            [InlineData("Microsoft.FooBar", "Microsoft.FooBar")]
            [InlineData("Microsoft.FooBar", "microsoft.foobar")]
            [InlineData("Microsoft.FooBar", " microsoft.foObar ")]
            private async Task WillThrowIfThePackageTitleMatchesAnExistingPackageRegistrationId(string existingRegistrationId, string newPackageTitle)
            {
                // Arrange
                var currentUser = new User();
                var existingPackageRegistration = new PackageRegistration
                {
                    Id = existingRegistrationId,
                    Owners = new HashSet<User>()
                };

                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                packageRegistrationRepository.Setup(r => r.GetAll()).Returns(new[] { existingPackageRegistration }.AsQueryable());

                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository);

                // Act
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(title: newPackageTitle);

                // Assert
                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false));

                Assert.Equal(String.Format(Strings.TitleMatchesExistingRegistration, newPackageTitle), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageIdIsLongerThanMaxPackageIdLength()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId".PadRight(131, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Id", CoreConstants.MaxPackageIdLength), ex.Message);
            }

            [Fact]
            private async Task DoesNotThrowIfTheNuGetPackageSpecialVersionContainsADot()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId", version: "1.2.3-alpha.0");

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false);
            }

            [Fact]
            private async Task DoesNotThrowIfTheNuGetPackageSpecialVersionContainsOnlyNumbers()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId", version: "1.2.3-12345");

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageAuthorsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(authors: "theFirstAuthor".PadRight(2001, '_') + ", " + "theSecondAuthor".PadRight(2001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Authors", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageCopyrightIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(copyright: "theCopyright".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheVersionIsLongerThan64Characters()
            {
                var service = CreateService();
                var versionString = "1.0.0-".PadRight(65, 'a');
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(version: versionString);

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Version", "64"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageDependenciesIsLongerThanInt16MaxValue()
            {
                var service = CreateService();
                var versionSpec = VersionRange.Parse("[1.0]");

                var numDependencies = 5000;
                var packageDependencies = new List<NuGet.Packaging.Core.PackageDependency>();
                for (int i = 0; i < numDependencies; i++)
                {
                    packageDependencies.Add(new NuGet.Packaging.Core.PackageDependency("dependency" + i, versionSpec));
                }

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(packageDependencyGroups: new[]
                {
                    new PackageDependencyGroup(
                        new NuGetFramework("net40"),
                        packageDependencies),
                });

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependencies", Int16.MaxValue), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfThePackageDependencyIdIsLongerThanMaxPackageIdLength()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(packageDependencyGroups: new[]
                    {
                        new PackageDependencyGroup(
                            new NuGetFramework("net40"),
                            new[]
                                {
                                     new NuGet.Packaging.Core.PackageDependency(
                                        "theFirstDependency".PadRight(129, '_'),
                                        new VersionRange(
                                            minVersion: new NuGetVersion("1.0")))
                                })
                    });

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependency.Id", CoreConstants.MaxPackageIdLength), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfThePackageDependencyVersionSpecIsLongerThan256()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(packageDependencyGroups: new[]
                    {
                        new PackageDependencyGroup(
                            new NuGetFramework("net40"),
                            new[]
                                {
                                     new NuGet.Packaging.Core.PackageDependency(
                                        "theFirstDependency",
                                        new VersionRange(
                                            minVersion: new NuGetVersion("1.0-".PadRight(257, 'a'))))
                                })
                    });

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependency.VersionSpec", 256), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageDescriptionIsNull()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(description: null);

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyMissing, "Description"), ex.Message);
            }


            [Fact]
            private async Task WillThrowIfTheNuGetPackageDescriptionIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(description: "theDescription".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Description", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageIconUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(iconUrl: new Uri("http://theIconUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageLicenseUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(licenseUrl: new Uri("http://theLicenseUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageProjectUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(projectUrl: new Uri("http://theProjectUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageSummaryIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(summary: "theSummary".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Summary", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageTagsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(tags: "theTags".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Tags", "4000"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageTitleIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(title: "theTitle".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Title", "256"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageLanguageIsLongerThan20()
            {
                // Arrange
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(language: new string('a', 21));

                // Act
                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                // Assert
                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Language", "20"), ex.Message);
            }

            [Fact]
            private async Task WillSaveSupportedFrameworks()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                {
                    mockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    mockPackageService.Setup(p => p.GetSupportedFrameworks(It.IsAny<PackageArchiveReader>())).Returns(
                        new[]
                        {
                                           NuGetFramework.Parse("net40"),
                                           NuGetFramework.Parse("net35")
                        });
                });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal("net40", package.SupportedFrameworks.First().TargetFramework);
                Assert.Equal("net35", package.SupportedFrameworks.ElementAt(1).TargetFramework);
            }

            [Fact]
            private async Task WillNotSaveAnySupportedFrameworksWhenThereIsAnAnyTargetFramework()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                {
                    mockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    mockPackageService.Setup(p => p.GetSupportedFrameworks(It.IsAny<PackageArchiveReader>())).Returns(
                        new[]
                        {
                            NuGetFramework.Parse("any"),
                            NuGetFramework.Parse("net35")
                        });
                });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Empty(package.SupportedFrameworks);
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

            [Theory]
            [InlineData(null)]
            [InlineData("2.0.0")]
            public void ReturnsTheLatestStableVersionIfAvailable(string semVerLevel)
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true, IsLatestStableSemVer2 = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("theId", version: null, semVerLevelKey: SemVerLevelKey.ForSemVerLevel(semVerLevel));

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0", result.Version);
            }

            [Fact]
            public void ReturnsTheLatestStableSemVer2VersionIfAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package0 = new Package { Version = "1.0.0+metadata", PackageRegistration = packageRegistration, Listed = true, IsLatestStableSemVer2 = true };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package0, package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("theId", version: null, semVerLevelKey: SemVerLevelKey.SemVer2);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0.0+metadata", result.Version);
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

        public class TheFindAbsoluteLatestPackageByIdMethod
        {
            [Fact]
            public void ReturnsTheLatestVersionWhenSemVerLevelUnknown()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                var package2 = new Package { Version = "2.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindAbsoluteLatestPackageById("theId", SemVerLevelKey.Unknown);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("2.0.0a", result.Version);
            }

            [Fact]
            public void ReturnsTheLatestVersionWhenSemVerLevel2()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                var package2 = new Package { Version = "2.0.0-alpha.1", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindAbsoluteLatestPackageById("theId", SemVerLevelKey.SemVer2);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("2.0.0-alpha.1", result.Version);
            }

            [Fact]
            public void ReturnsTheMostRecentVersionWhenSemVerLevelUnknown()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true };
                var package2 = new Package { Version = "2.0.0-alpha", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true };
                var package3 = new Package { Version = "2.0.0", PackageRegistration = packageRegistration, Listed = true, IsLatest = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2, package3 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindAbsoluteLatestPackageById("theId", SemVerLevelKey.Unknown);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("2.0.0", result.Version);
            }

            [Fact]
            public void ReturnsTheMostRecentVersionWhenSemVerLevel2()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true };
                var package2 = new Package { Version = "2.0.0-alpha.1", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, SemVerLevelKey = SemVerLevelKey.SemVer2 };
                var package3 = new Package { Version = "2.0.0+metadata", PackageRegistration = packageRegistration, Listed = true, SemVerLevelKey = SemVerLevelKey.SemVer2, IsLatestSemVer2 = true };

                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(new[] { package1, package2, package3 }.AsQueryable());
                var service = CreateService(packageRepository: repository);

                // Act
                var result = service.FindAbsoluteLatestPackageById("theId", SemVerLevelKey.SemVer2);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("2.0.0+metadata", result.Version);
            }
        }

        public class TheFindPackagesByOwnerMethod : TheFindPackagesByOwnersMethodsBase
        {
            public static IEnumerable<object[]> TestData_RoleVariants
            {
                get
                {
                    var roles = TheFindPackagesByOwnersMethodsBase.CreateTestUserRoles();

                    yield return new object[] { roles.Admin, roles.Admin };
                    yield return new object[] { roles.Organization, roles.Organization };
                }
            }

            public override IEnumerable<Package> InvokeFindPackagesByOwner(User user, bool includeUnlisted, bool includeVersions = false)
            {
                return PackageService.FindPackagesByOwner(user, includeUnlisted, includeVersions);
            }

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsAListedPackage(User currentUser, User packageOwner)
              => base.ReturnsAListedPackage(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsNoUnlistedPackagesWhenIncludeUnlistedIsFalse(User currentUser, User packageOwner)
              => base.ReturnsNoUnlistedPackagesWhenIncludeUnlistedIsFalse(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsAnUnlistedPackageWhenIncludeUnlistedIsTrue(User currentUser, User packageOwner)
              => base.ReturnsAnUnlistedPackageWhenIncludeUnlistedIsTrue(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsAPackageForEachPackageRegistration(User currentUser, User packageOwner)
              => base.ReturnsAPackageForEachPackageRegistration(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsOnlyLatestStableSemVer2PackageIfBothExist(User currentUser, User packageOwner)
              => base.ReturnsOnlyLatestStableSemVer2PackageIfBothExist(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsOnlyLatestStablePackageIfNoLatestStableSemVer2Exist(User currentUser, User packageOwner)
              => base.ReturnsOnlyLatestStablePackageIfNoLatestStableSemVer2Exist(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsFirstIfMultiplePackagesSetToLatest(User currentUser, User packageOwner)
              => base.ReturnsFirstIfMultiplePackagesSetToLatest(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedTrue(User currentUser, User packageOwner)
              => base.ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedTrue(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedFalse(User currentUser, User packageOwner)
              => base.ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedFalse(currentUser, packageOwner);
        }

        public class TheFindPackagesByAnyMatchingOwnerMethod : TheFindPackagesByOwnersMethodsBase
        {
            public static IEnumerable<object[]> TestData_RoleVariants
            {
                get
                {
                    var roles = TheFindPackagesByOwnersMethodsBase.CreateTestUserRoles();

                    yield return new object[] { roles.Admin, roles.Admin };
                    yield return new object[] { roles.Admin, roles.Organization };
                    yield return new object[] { roles.Collaborator, roles.Organization };
                }
            }

            public override IEnumerable<Package> InvokeFindPackagesByOwner(User user, bool includeUnlisted, bool includeVersions = false)
            {
                return PackageService.FindPackagesByAnyMatchingOwner(user, includeUnlisted, includeVersions);
            }

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsAListedPackage(User currentUser, User packageOwner)
              => base.ReturnsAListedPackage(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsNoUnlistedPackagesWhenIncludeUnlistedIsFalse(User currentUser, User packageOwner)
              => base.ReturnsNoUnlistedPackagesWhenIncludeUnlistedIsFalse(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsAnUnlistedPackageWhenIncludeUnlistedIsTrue(User currentUser, User packageOwner)
              => base.ReturnsAnUnlistedPackageWhenIncludeUnlistedIsTrue(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsAPackageForEachPackageRegistration(User currentUser, User packageOwner)
              => base.ReturnsAPackageForEachPackageRegistration(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsOnlyLatestStableSemVer2PackageIfBothExist(User currentUser, User packageOwner)
              => base.ReturnsOnlyLatestStableSemVer2PackageIfBothExist(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsOnlyLatestStablePackageIfNoLatestStableSemVer2Exist(User currentUser, User packageOwner)
              => base.ReturnsOnlyLatestStablePackageIfNoLatestStableSemVer2Exist(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsFirstIfMultiplePackagesSetToLatest(User currentUser, User packageOwner)
              => base.ReturnsFirstIfMultiplePackagesSetToLatest(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedTrue(User currentUser, User packageOwner)
              => base.ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedTrue(currentUser, packageOwner);

            [MemberData(nameof(TestData_RoleVariants))]
            public override void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedFalse(User currentUser, User packageOwner)
              => base.ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedFalse(currentUser, packageOwner);
        }

        public abstract class TheFindPackagesByOwnersMethodsBase : TestContainer
        {
            public abstract IEnumerable<Package> InvokeFindPackagesByOwner(User user, bool includeUnlisted, bool includeVersions = false);

            protected class TestUserRoles
            {
                public User Admin { get; set; }
                public User Collaborator { get; set; }
                public User Organization { get; set; }
            }

            protected static TestUserRoles CreateTestUserRoles()
            {
                var organization = new Organization { Key = 0, Username = "organization" };

                var admin = new User { Key = 1, Username = "admin" };
                var adminMembership = new Membership
                {
                    Organization = organization,
                    Member = admin,
                    IsAdmin = true
                };
                organization.Members.Add(adminMembership);
                admin.Organizations.Add(adminMembership);

                var collaborator = new User { Key = 2, Username = "collaborator" };
                var collaboratorMembership = new Membership
                {
                    Organization = organization,
                    Member = admin,
                    IsAdmin = false
                };
                organization.Members.Add(collaboratorMembership);
                collaborator.Organizations.Add(collaboratorMembership);

                return new TestUserRoles
                {
                    Organization = organization,
                    Admin = admin,
                    Collaborator = collaborator
                };
            }

            protected IPackageService PackageService
            {
                get
                {
                    return GetService<PackageService>();
                }
            }

            [Theory]
            public virtual void ReturnsAListedPackage(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestSemVer2 = true, IsLatestStableSemVer2 = true };
                packageRegistration.Packages.Add(package);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false);
                Assert.Equal(1, packages.Count());
            }

            [Theory]
            public virtual void ReturnsNoUnlistedPackagesWhenIncludeUnlistedIsFalse(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false, IsLatest = false, IsLatestStable = false };
                packageRegistration.Packages.Add(package);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false);
                Assert.Equal(0, packages.Count());
            }

            [Theory]
            public virtual void ReturnsAnUnlistedPackageWhenIncludeUnlistedIsTrue(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false, IsLatest = false, IsLatestStable = false };
                packageRegistration.Packages.Add(package);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: true);
                Assert.Equal(1, packages.Count());
            }

            [Theory]
            public virtual void ReturnsAPackageForEachPackageRegistration(User currentUser, User packageOwner)
            {
                var packageRegistrationA = new PackageRegistration { Key = 0, Id = "idA", Owners = { packageOwner } };
                var packageRegistrationB = new PackageRegistration { Key = 1, Id = "idB", Owners = { packageOwner } };
                var packageA = new Package {
                    Version = "1.0",
                    PackageRegistration = packageRegistrationA,
                    PackageRegistrationKey = 0,
                    Listed = true,
                    IsLatestSemVer2 = true,
                    IsLatestStableSemVer2 = true
                };
                var packageB = new Package {
                    Version = "1.0",
                    PackageRegistration = packageRegistrationB,
                    PackageRegistrationKey = 1,
                    Listed = true,
                    IsLatestSemVer2 = true,
                    IsLatestStableSemVer2 = true
                };
                packageRegistrationA.Packages.Add(packageA);
                packageRegistrationB.Packages.Add(packageB);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistrationA);
                context.PackageRegistrations.Add(packageRegistrationB);
                context.Packages.Add(packageA);
                context.Packages.Add(packageB);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false).ToList();
                Assert.Equal(2, packages.Count);
                Assert.Contains(packageA, packages);
                Assert.Contains(packageB, packages);
            }

            [Theory]
            public virtual void ReturnsOnlyLatestStableSemVer2PackageIfBothExist(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var latestPackage = new Package { Version = "2.0.0-alpha", PackageRegistration = packageRegistration, Listed = true, IsLatest = true };
                var latestSemVer2Package = new Package { Version = "2.0.0-alpha.1", PackageRegistration = packageRegistration, Listed = true, IsLatestSemVer2 = true };
                var latestStablePackage = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStableSemVer2 = true };
                packageRegistration.Packages.Add(latestPackage);
                packageRegistration.Packages.Add(latestStablePackage);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(latestPackage);
                context.Packages.Add(latestStablePackage);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false).ToList();
                Assert.Equal(1, packages.Count);
                Assert.Contains(latestStablePackage, packages);
            }

            [Theory]
            public virtual void ReturnsOnlyLatestStablePackageIfNoLatestStableSemVer2Exist(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var latestPackage = new Package { Version = "2.0.0-alpha", PackageRegistration = packageRegistration, Listed = true, IsLatest = true };
                var latestStablePackage = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                packageRegistration.Packages.Add(latestPackage);
                packageRegistration.Packages.Add(latestStablePackage);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(latestPackage);
                context.Packages.Add(latestStablePackage);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false).ToList();
                Assert.Equal(1, packages.Count);
                Assert.Contains(latestStablePackage, packages);
            }

            protected FakeEntitiesContext GetMixedVersioningPackagesContext(User currentUser, User packageOwner)
            {
                var context = GetFakeContext();

                context.Users.Add(currentUser);

                var sleetLibRegistration = new PackageRegistration { Id = "SleetLib", Owners = { packageOwner } };
                var sleetLibPackages = new[]
                {
                    new Package { Version = "2.2.24+git.f2a0cb6", PackageRegistration = sleetLibRegistration, Listed = true, IsLatestStableSemVer2 = true, IsLatestSemVer2 = true },
                    new Package { Version = "2.2.18+git.4d361d8", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.16+git.c6be4b4", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.13+git.e657e80", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.9+git.4a81f0c", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.7+git.393c301", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.3+git.98f8237", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.1+git.e11393a", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.2.0+git.6973dc7", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.0.0+git.5106315", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { Version = "2.0.0-beta.19+git.hash.befdb81dbbef6fb5b8cdf147cc467f9904339cc8", PackageRegistration = sleetLibRegistration, Listed = false },
                    new Package { Version = "1.1.0-beta-296", PackageRegistration = sleetLibRegistration, Listed = true, IsLatest = true }
                };
                context.PackageRegistrations.Add(sleetLibRegistration);
                foreach (var package in sleetLibPackages)
                {
                    context.Packages.Add(package);
                }

                var nugetCatalogReaderRegistration = new PackageRegistration { Id = "NuGet.CatalogReader", Owners = { packageOwner } };
                var nugetCatalogReaderPackages = new[]
                {
                    new Package { Version = "1.5.12+git.78e44a8", PackageRegistration = nugetCatalogReaderRegistration, Listed = true, IsLatestStableSemVer2 = true, IsLatestSemVer2 = true },
                    new Package { Version = "1.5.8+git.bcda3b8", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { Version = "1.4.0+git.e2a36b6", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { Version = "1.3.0+git.a6a89a3", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { Version = "1.2.0", PackageRegistration = nugetCatalogReaderRegistration, Listed = true, IsLatest = true, IsLatestStable = true },
                    new Package { Version = "1.1.0", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { Version = "1.0.0", PackageRegistration = nugetCatalogReaderRegistration, Listed = true }
                };
                context.PackageRegistrations.Add(nugetCatalogReaderRegistration);
                foreach (var package in nugetCatalogReaderPackages)
                {
                    context.Packages.Add(package);
                }

                return context;
            }

            [Theory]
            public virtual void ReturnsFirstIfMultiplePackagesSetToLatest(User currentUser, User packageOwner)
            {
                // Verify behavior to work around IsLatest concurrency issue: https://github.com/NuGet/NuGetGallery/issues/2514
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatest = true, IsLatestStable = true };
                var package2 = new Package { Version = "2.0", PackageRegistration = packageRegistration, Listed = true, IsLatest = true, IsLatestStable = true };
                packageRegistration.Packages.Add(package2);
                packageRegistration.Packages.Add(package1);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package2);
                context.Packages.Add(package1);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false);
                Assert.Equal(1, packages.Count());
                Assert.Contains(package2, packages);
            }

            [Theory]
            public virtual void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedTrue(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Key = 0, Id = "theId", Owners = { packageOwner } };

                var package1 = new Package {
                    Version = "1.0",
                    PackageRegistration = packageRegistration,
                    PackageRegistrationKey = 0,
                    Listed = false,
                    IsLatest = false,
                    IsLatestStable = false
                };
                packageRegistration.Packages.Add(package1);

                var package2 = new Package {
                    Version = "2.0",
                    PackageRegistration = packageRegistration,
                    PackageRegistrationKey = 0,
                    Listed = true,
                    IsLatest = true,
                    IsLatestStable = true
                };
                packageRegistration.Packages.Add(package2);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package1);
                context.Packages.Add(package2);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: true, includeVersions: true);
                Assert.Equal(2, packages.Count());
            }

            [Theory]
            public virtual void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedFalse(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Id = "theId", Owners = { packageOwner } };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false, IsLatest = false, IsLatestStable = false };
                packageRegistration.Packages.Add(package1);

                var package2 = new Package { Version = "2.0", PackageRegistration = packageRegistration, Listed = true, IsLatest = true, IsLatestStable = true };
                packageRegistration.Packages.Add(package2);

                var context = GetFakeContext();
                context.Users.Add(currentUser);
                context.PackageRegistrations.Add(packageRegistration);
                context.Packages.Add(package1);
                context.Packages.Add(package2);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: false, includeVersions: true);
                Assert.Equal(1, packages.Count());
            }
        }

        public class TheMarkPackageListedMethod
        {
            [Fact]
            public async Task SetsListedToTrue()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                await service.MarkPackageListedAsync(package);

                Assert.True(package.Listed);
            }

            [Fact]
            public async Task DoNotCommitIfCommitChangesIsFalse()
            {
                // Assert
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.MarkPackageListedAsync(package, commitChanges: false);

                // Assert
                packageRepository.Verify(p => p.CommitChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task CommitIfCommitChangesIsTrue()
            {
                // Assert
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.MarkPackageListedAsync(package, commitChanges: true);

                // Assert
                packageRepository.Verify(p => p.CommitChangesAsync(), Times.Once());
            }

            [Fact]
            public async Task OnPackageVersionHigherThanLatestSetsItToLatestVersion()
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

                await service.MarkPackageListedAsync(packages[0]);

                Assert.True(packageRegistration.Packages.ElementAt(0).IsLatest);
                Assert.True(packageRegistration.Packages.ElementAt(0).IsLatestStable);
                Assert.False(packages.ElementAt(1).IsLatest);
                Assert.False(packages.ElementAt(1).IsLatestStable);
            }


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
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.MarkPackageListedAsync(package));
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var auditingService = new TestAuditingService();
                var service = CreateService(
                    packageRepository: packageRepository,
                    auditingService: auditingService);

                // Act
                await service.MarkPackageListedAsync(package);

                // Assert
                Assert.True(auditingService.WroteRecord<PackageAuditRecord>(ar =>
                    ar.Action == AuditedPackageAction.List
                    && ar.Id == package.PackageRegistration.Id
                    && ar.Version == package.Version));
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var telemetryService = new Mock<ITelemetryService>();
                var service = CreateService(telemetryService: telemetryService);

                await service.MarkPackageListedAsync(package);

                telemetryService.Verify(
                    x => x.TrackPackageListed(package),
                    Times.Once);
            }
        }

        public class TheMarkPackageUnlistedMethod
        {
            [Fact]
            public async Task SetsListedToFalse()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                await service.MarkPackageUnlistedAsync(package);

                Assert.False(package.Listed);
            }

            [Fact]
            public async Task CommitIfCommitChangesIfTrue()
            {
                // Act
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.MarkPackageUnlistedAsync(package, commitChanges: true);

                // Assert
                packageRepository.Verify(p => p.CommitChangesAsync(), Times.Once());
            }

            [Fact]
            public async Task DoNotCommitIfCommitChangesIfFalse()
            {
                // Act
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                // Act
                await service.MarkPackageUnlistedAsync(package, commitChanges: false);

                // Assert
                packageRepository.Verify(p => p.CommitChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task OnLatestPackageVersionSetsPreviousToLatestVersion()
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

                await service.MarkPackageUnlistedAsync(packages[0]);

                Assert.False(packageRegistration.Packages.ElementAt(0).IsLatest);
                Assert.False(packageRegistration.Packages.ElementAt(0).IsLatestStable);
                Assert.True(packages.ElementAt(1).IsLatest);
                Assert.True(packages.ElementAt(1).IsLatestStable);
            }

            [Fact]
            public async Task OnOnlyListedPackageSetsNoPackageToLatestVersion()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0.1", PackageRegistration = packageRegistration, IsLatest = true, IsLatestStable = true };
                packageRegistration.Packages = new List<Package>(new[] { package });
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository);

                await service.MarkPackageUnlistedAsync(package);

                Assert.False(package.IsLatest, "IsLatest");
                Assert.False(package.IsLatestStable, "IsLatestStable");
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var auditingService = new TestAuditingService();
                var service = CreateService(
                    packageRepository: packageRepository,
                    auditingService: auditingService);

                // Act
                await service.MarkPackageUnlistedAsync(package);

                // Assert
                Assert.True(auditingService.WroteRecord<PackageAuditRecord>(ar =>
                    ar.Action == AuditedPackageAction.Unlist
                    && ar.Id == package.PackageRegistration.Id
                    && ar.Version == package.Version));
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true };
                var telemetryService = new Mock<ITelemetryService>();
                var service = CreateService(telemetryService: telemetryService);

                await service.MarkPackageUnlistedAsync(package);

                telemetryService.Verify(
                    x => x.TrackPackageUnlisted(package),
                    Times.Once);
            }
        }

        public class ThePublishPackageMethod
        {
            [Fact]
            public async Task WillSetThePublishedDateOnThePackageBeingPublished()
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
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync("theId", "1.0.42");

                Assert.NotNull(package.Published);
                packageRepository.Verify(x => x.CommitChangesAsync());
            }

            [Fact]
            public async Task WillSetThePublishedDateOnThePackageBeingPublishedWithOverload()
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
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync(package, commitChanges: false);

                Assert.NotNull(package.Published);
                packageRepository.Verify(x => x.CommitChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WillSetUpdateIsLatestStableOnThePackageWhenItIsTheLatestVersionWithOverload()
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
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync(package);

                Assert.True(package.IsLatestStable);
            }

            [Fact]
            public async Task WillSetUpdateIsLatestStableOnThePackageWhenItIsTheLatestVersion()
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
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync("theId", "1.0.42");

                Assert.True(package.IsLatestStable);
            }

            [Fact]
            public async Task WillNotSetUpdateIsLatestStableOnThePackageWhenItIsNotTheLatestVersionWithOverload()
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
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync(package);

                Assert.False(package.IsLatestStable);
            }

            [Fact]
            public async Task PublishPackageUpdatesIsAbsoluteLatestForPrereleasePackage()
            {
                var package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.UtcNow,
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
                    Published = DateTime.UtcNow.AddDays(-1)
                };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync("theId", "1.0.42-alpha");
                Assert.True(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public async Task PublishPackageUpdatesIsAbsoluteLatestForPrereleasePackageWithOverload()
            {
                var package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.UtcNow,
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
                    Published = DateTime.UtcNow.AddDays(-1)
                };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync(package);

                Assert.True(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public async Task SetUpdateDoesNotSetIsLatestStableForAnyIfAllPackagesArePrerelease()
            {
                var package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.UtcNow,
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
                    Published = DateTime.UtcNow.AddDays(-1),
                    IsPrerelease = true
                };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync("theId", "1.0.42-alpha");
                Assert.False(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public async Task SetUpdateDoesNotSetIsLatestStableForAnyIfAllPackagesArePrereleaseWithOverload()
            {
                var package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.UtcNow,
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
                    Published = DateTime.UtcNow.AddDays(-1),
                    IsPrerelease = true
                };
                package.PackageRegistration.Packages.Add(package39);
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepository: packageRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package); });

                await service.PublishPackageAsync(package);
                Assert.False(package39.IsLatestStable);
                Assert.False(package39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public async Task WillThrowIfThePackageDoesNotExist()
            {
                var service = CreateService(setup:
                        mockPackageService =>
                        {
                            mockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                                (Package)null);
                        });

                var ex = await Assert.ThrowsAsync<EntityException>(async () => await service.PublishPackageAsync("theId", "1.0.42"));

                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfThePackageIsNull()
            {
                var service = CreateService();

                await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.PublishPackageAsync(null));
            }
        }

        public class TheRemovePackageOwnerMethod
        {
            [Fact]
            public async Task RemovesPackageOwner()
            {
                var service = CreateService();
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Owners = new List<User> { owner1, owner2 } };

                await service.RemovePackageOwnerAsync(package, owner1);

                Assert.DoesNotContain(owner1, package.Owners);
            }

            [Fact]
            public async Task WontRemoveLastOwner()
            {
                var service = CreateService();
                var singleOwner = new User { Key = 1, Username = "Owner" };
                var package = new PackageRegistration { Owners = new List<User> { singleOwner } };

                await service.RemovePackageOwnerAsync(package, singleOwner);

                Assert.DoesNotContain(singleOwner, package.Owners);
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

                service.SetLicenseReportVisibilityAsync(package, true);

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

                service.SetLicenseReportVisibilityAsync(package, false);

                Assert.True(package.HideLicenseReport);
            }
        }

        public class TheGetPackageUserAccountOwnersMethod : TestContainer
        {
            [Theory]
            [MemberData(nameof(GetPackageUserAccountOwners_Input))]
            public void TestGetPackageUserAccountOwners(Package package, int expectedResult)
            {
                // Arrange
                var context = GetFakeContext();
                context.PackageRegistrations.Add(package.PackageRegistration);
                context.Packages.Add(package);
                var service = Get<PackageService>();

                // Act
                var result = service.GetPackageUserAccountOwners(package).Count();

                // Assert
                Assert.Equal(expectedResult, result);
            }

            static PackageRegistration CreatePackageRegistration(int key)
            {
                return new PackageRegistration() { Key = 1, Id = $"regKey{key}" };
            }

            public static IEnumerable<object[]> GetPackageUserAccountOwners_Input
            {
                get
                {
                    List<object[]> result = new List<object[]>();

                    var description = "Description";
                    var packageRegistration0 = CreatePackageRegistration(0);
                    var result0 = 0;
                    result.Add(new object[] { new Package() { Key = 0, Version = "1.0.0", PackageRegistration = packageRegistration0, Description = description }, result0 });

                    var packageRegistration1 = CreatePackageRegistration(1);
                    packageRegistration1.Owners.Add(new User() { Username = "user1", Key = 1 });
                    var result1 = 1;
                    result.Add(new object[] { new Package() { Key = 1, Version = "1.0.0", PackageRegistration = packageRegistration1, Description = description }, result1 });

                    var packageRegistration2 = CreatePackageRegistration(2);
                    packageRegistration2.Owners.Add(new User() { Username = "user2.1", Key = 1 });
                    packageRegistration2.Owners.Add(new User() { Username = "user2.2", Key = 2 });
                    var result2 = 2;
                    result.Add(new object[] { new Package() { Key = 2, Version = "1.0.0", PackageRegistration = packageRegistration2, Description = description }, result2 });

                    var packageRegistration3 = CreatePackageRegistration(3);
                    packageRegistration3.Owners.Add(new Organization() { Username = "userOrg3", Members = new List<Membership>() });
                    var result3 = 0;
                    result.Add(new object[] { new Package() { Key = 3, Version = "1.0.0", PackageRegistration = packageRegistration3, Description = description }, result3 });

                    var packageRegistration4 = CreatePackageRegistration(4);
                    packageRegistration4.Owners.Add(new User() { Username = "user4.1" });
                    packageRegistration4.Owners.Add(new Organization() { Username = "userOrg4" });
                    var result4 = 1;
                    result.Add(new object[] { new Package() { Key = 4, Version = "1.0.0", PackageRegistration = packageRegistration4, Description = description }, result4 });

                    // A single organization with one owner
                    var packageRegistration5 = CreatePackageRegistration(5);
                    var user51 = new User() { Username = "user5.1", Key = 51 };
                    packageRegistration5.Owners.Add(new Organization()
                    {
                        Username = "userOrg5",
                        Key = 50,
                        Members = new List<Membership> { new Membership() { Member = user51, MemberKey = user51.Key, OrganizationKey = 50 } }
                    });
                    var result5 = 1;
                    result.Add(new object[] { new Package() { Key = 5, Version = "1.0.0", PackageRegistration = packageRegistration5, Description = description }, result5 });

                    // Same user in organization and as individual account
                    var packageRegistration6 = CreatePackageRegistration(6);
                    var user61 = new User() { Username = "user6.1", Key = 61 };
                    packageRegistration6.Owners.Add(new Organization()
                    {
                        Username = "userOrg6",
                        Key = 60,
                        Members = new List<Membership> { new Membership() { Member = user61, MemberKey = user61.Key, OrganizationKey = 60 } }
                    });
                    packageRegistration6.Owners.Add(user61);
                    var result6 = 1;
                    result.Add(new object[] { new Package() { Key = 6, Version = "1.0.0", PackageRegistration = packageRegistration6, Description = description }, result6 });

                    // One organization with two members
                    var packageRegistration7 = CreatePackageRegistration(7);
                    var user71 = new User() { Username = "user7.1", Key = 71 };
                    var user72 = new User() { Username = "user7.2", Key = 72 };
                    packageRegistration7.Owners.Add(new Organization()
                    {
                        Username = "userOrg7",
                        Key = 70,
                        Members = new List<Membership>{new Membership(){Member = user71, MemberKey = user71.Key, OrganizationKey = 70},
                                                       new Membership(){Member = user72, MemberKey = user72.Key, OrganizationKey = 70}}
                    });
                    var result7 = 2;
                    result.Add(new object[] { new Package() { Key = 7, Version = "1.0.0", PackageRegistration = packageRegistration7, Description = description }, result7 });

                    // Two organizations with same member
                    var packageRegistration8 = CreatePackageRegistration(9);
                    var user81 = new User() { Username = "user8.1", Key = 81 };
                    packageRegistration8.Owners.Add(new Organization()
                    {
                        Username = "userOrg81",
                        Key = 801,
                        Members = new List<Membership> { new Membership() { Member = user81, MemberKey = user81.Key, OrganizationKey = 801 } }
                    });
                    packageRegistration8.Owners.Add(new Organization()
                    {
                        Username = "userOrg82",
                        Key = 802,
                        Members = new List<Membership> { new Membership() { Member = user81, MemberKey = user81.Key, OrganizationKey = 802 } }
                    });
                    var result8 = 1;
                    result.Add(new object[] { new Package() { Key = 8, Version = "1.0.0", PackageRegistration = packageRegistration8, Description = description }, result8 });

                    // Organization with suborganization with one member 
                    var packageRegistration9 = CreatePackageRegistration(9);
                    var user91 = new User() { Username = "user9.1", Key = 91 };
                    var org91 = new Organization()
                    {
                        Username = "org9Child",
                        Key = 902,
                        Members = new List<Membership> { new Membership() { Member = user91, MemberKey = user91.Key, OrganizationKey = 902 } }
                    };
                    packageRegistration9.Owners.Add(new Organization()
                    {
                        Username = "userOrgParent",
                        Key = 901,
                        Members = new List<Membership> { new Membership() { Member = org91, MemberKey = org91.Key, OrganizationKey = 901 } }
                    });
                    var result9 = 1;
                    result.Add(new object[] { new Package() { Key = 9, Version = "1.0.0", PackageRegistration = packageRegistration9, Description = description }, result9 });

                    // Organization with suborganization with one member and one individual user account
                    var packageRegistration10 = CreatePackageRegistration(10);
                    var user101 = new User() { Username = "user10.1", Key = 101 };
                    var org101 = new Organization()
                    {
                        Username = "org101Child",
                        Key = 1002,
                        Members = new List<Membership> { new Membership() { Member = user101, MemberKey = user101.Key, OrganizationKey = 1002 } }
                    };
                    packageRegistration10.Owners.Add(new Organization()
                    {
                        Username = "userOrgParent",
                        Key = 1001,
                        Members = new List<Membership> { new Membership() { Member = org101, MemberKey = org101.Key, OrganizationKey = 1001 } }
                    });
                    packageRegistration10.Owners.Add(user101);
                    var result10 = 1;
                    result.Add(new object[] { new Package() { Key = 10, Version = "1.0.0", PackageRegistration = packageRegistration10, Description = description }, result10 });

                    // Organization with suborganization with one member and one individual different user account
                    var packageRegistration11 = CreatePackageRegistration(11);
                    var user111 = new User() { Username = "user11.1", Key = 111 };
                    var user112 = new User() { Username = "user11.2", Key = 112 };
                    var org111 = new Organization()
                    {
                        Username = "org111Child",
                        Key = 1102,
                        Members = new List<Membership> { new Membership() { Member = user111, MemberKey = user111.Key, OrganizationKey = 1102 } }
                    };
                    packageRegistration11.Owners.Add(new Organization()
                    {
                        Username = "userOrgParent",
                        Key = 1101,
                        Members = new List<Membership> { new Membership() { Member = org111, MemberKey = org111.Key, OrganizationKey = 1101 } }
                    });
                    packageRegistration11.Owners.Add(user112);
                    var result11 = 2;
                    result.Add(new object[] { new Package() { Key = 11, Version = "1.0.0", PackageRegistration = packageRegistration11, Description = description }, result11 });

                    return result;
                }
            }
        }
    }
}