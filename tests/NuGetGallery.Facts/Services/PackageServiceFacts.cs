// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class PackageServiceFacts
    {
        private static IPackageService CreateService(
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<Package>> packageRepository = null,
            Mock<IEntityRepository<Certificate>> certificateRepository = null,
            IAuditingService auditingService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<ISecurityPolicyService> securityPolicyService = null,
            Action<Mock<PackageService>> setup = null)
        {
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            certificateRepository = certificateRepository ?? new Mock<IEntityRepository<Certificate>>();
            auditingService = auditingService ?? new TestAuditingService();
            telemetryService = telemetryService ?? new Mock<ITelemetryService>();
            securityPolicyService = securityPolicyService ?? new Mock<ISecurityPolicyService>();

            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                certificateRepository.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object);

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
                var securityPolicyService = new Mock<ISecurityPolicyService>();
                packageRepository.Setup(r => r.CommitChangesAsync())
                    .Returns(Task.CompletedTask).Verifiable();
                securityPolicyService.Setup(x => x.IsSubscribed(
                        It.Is<User>(u => u == pendingOwner),
                        It.Is<string>(p => p == AutomaticallyOverwriteRequiredSignerPolicy.PolicyName)))
                    .Returns(false);

                var service = CreateService(
                    packageRepository: packageRepository,
                    securityPolicyService: securityPolicyService);

                await service.AddPackageOwnerAsync(package, pendingOwner);

                Assert.Contains(pendingOwner, package.Owners);
                packageRepository.VerifyAll();
            }

            [Fact]
            public async Task WhenNewOwnerHasAutomaticallyOverwriteRequiredSignerPolicyAndRequiredSignerIsNull_NewOwnerBecomesRequiredSigner()
            {
                var packageRegistration = new PackageRegistration()
                {
                    Key = 1,
                    Id = "a"
                };
                var newOwner = new User()
                {
                    Key = 2,
                    Username = "b"
                };
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var securityPolicyService = new Mock<ISecurityPolicyService>();

                packageRepository.Setup(pr => pr.CommitChangesAsync())
                    .Returns(Task.CompletedTask);
                securityPolicyService.Setup(x => x.IsSubscribed(
                        It.Is<User>(u => u == newOwner),
                        It.Is<string>(p => p == AutomaticallyOverwriteRequiredSignerPolicy.PolicyName)))
                    .Returns(true);

                var packageService = CreateService(
                    packageRepository: packageRepository,
                    securityPolicyService: securityPolicyService);

                await packageService.AddPackageOwnerAsync(packageRegistration, newOwner);

                Assert.Equal(1, packageRegistration.RequiredSigners.Count);
                Assert.Contains(newOwner, packageRegistration.RequiredSigners);

                packageRepository.VerifyAll();
                securityPolicyService.VerifyAll();
            }

            [Fact]
            public async Task WhenNewOwnerHasAutomaticallyOverwriteRequiredSignerPolicyAndRequiredSignerIsNotNull_NewOwnerBecomesRequiredSigner()
            {
                var packageRegistration = new PackageRegistration()
                {
                    Key = 1,
                    Id = "a"
                };
                var existingOwner = new User()
                {
                    Key = 2,
                    Username = "b"
                };
                var newOwner = new User()
                {
                    Key = 3,
                    Username = "c"
                };

                packageRegistration.Owners.Add(existingOwner);
                packageRegistration.RequiredSigners.Add(existingOwner);

                var packageRepository = new Mock<IEntityRepository<Package>>();
                var securityPolicyService = new Mock<ISecurityPolicyService>();

                packageRepository.Setup(pr => pr.CommitChangesAsync())
                    .Returns(Task.CompletedTask);
                securityPolicyService.Setup(x => x.IsSubscribed(
                        It.Is<User>(u => u == newOwner),
                        It.Is<string>(p => p == AutomaticallyOverwriteRequiredSignerPolicy.PolicyName)))
                    .Returns(true);

                var packageService = CreateService(
                    packageRepository: packageRepository,
                    securityPolicyService: securityPolicyService);

                await packageService.AddPackageOwnerAsync(packageRegistration, newOwner);

                Assert.Equal(1, packageRegistration.RequiredSigners.Count);
                Assert.Contains(newOwner, packageRegistration.RequiredSigners);

                packageRepository.VerifyAll();
                securityPolicyService.VerifyAll();
            }
        }

        public class TheCreatePackageMethod
        {
            [Fact]
            public async Task WillCreateANewPackageRegistrationUsingTheNugetPackIdWhenOneDoesNotAlreadyExist()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                {
                    mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                });

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Id == "theId")));
            }

            [Fact]
            public async Task WillMakeTheCurrentUserTheOwnerWhenCreatingANewPackageRegistration()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                packageRegistrationRepository.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Owners.Contains(currentUser))));
            }

            [Fact]
            public async Task WillReadThePropertiesFromTheNuGetPackageWhenCreatingANewPackage()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(
                    licenseUrl: new Uri("http://thelicenseurl/"),
                    projectUrl: new Uri("http://theprojecturl/"),
                    iconUrl: new Uri("http://theiconurl/"),
                    licenseFilename: "license.txt");
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
                Assert.True(package.RequiresLicenseAcceptance);
                Assert.True(package.DevelopmentDependency);
                Assert.Equal("theSummary", package.Summary);
                Assert.Equal("theTags", package.Tags);
                Assert.Equal("theTitle", package.Title);
                Assert.Equal("theCopyright", package.Copyright);
                Assert.Equal(EmbeddedLicenseFileType.PlainText, package.EmbeddedLicenseType);
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

            [Theory]
            [InlineData(true, true)]
            [InlineData(false, false)]
            [InlineData(null, false)]
            public async Task WillReadDevelopmentDependencyFromPackage(bool? developmentDependency, bool expected)
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(developmentDependency: developmentDependency);

                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                // Assert
                Assert.Equal(expected, package.DevelopmentDependency);
            }

            [Fact]
            public async Task WillThrowIfDevelopmentDependencyIsInvalid()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                    mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(
                    getCustomNuspecNodes: () => "<developmentDependency>foo</developmentDependency>");

                var currentUser = new User();

                // Assert
                var exception = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false));
                Assert.Contains("developmentDependency", exception.Message);
            }

            [Fact]
            public async Task WillReadRepositoryMetadataPropertyFromThePackage()
            {
                // Arrange
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });

                var repositoryMetadata = new NuGet.Packaging.Core.RepositoryMetadata()
                {
                    Type = "git",
                    Url = "https://github.com/NuGet/NuGetGallery",
                };

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(repositoryMetadata: repositoryMetadata);

                var currentUser = new User();

                // Act
                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                // Assert
                Assert.Equal(repositoryMetadata.Type, package.RepositoryType);
                Assert.Equal(repositoryMetadata.Url, package.RepositoryUrl);
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

            [Theory]
            [InlineData(null, EmbeddedLicenseFileType.Absent)]
            [InlineData("foo.txt", EmbeddedLicenseFileType.PlainText)]
            [InlineData("bar.md", EmbeddedLicenseFileType.Markdown)]
            [InlineData("foo.tXt", EmbeddedLicenseFileType.PlainText)]
            [InlineData("bar.mD", EmbeddedLicenseFileType.Markdown)]
            [InlineData("baz", EmbeddedLicenseFileType.PlainText)]
            public async Task WillDetectLicenseFileType(string licenseFileName, EmbeddedLicenseFileType expectedFileType)
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(
                    licenseUrl: new Uri("http://thelicenseurl/"),
                    projectUrl: new Uri("http://theprojecturl/"),
                    iconUrl: new Uri("http://theiconurl/"),
                    licenseFilename: licenseFileName);
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal(expectedFileType, package.EmbeddedLicenseType);
                Assert.Null(package.LicenseExpression);
            }

            [Fact]
            public async Task WillSaveLicenseExpression()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                const string licenseExpressionText = "some license expression text";
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(
                    licenseUrl: new Uri("http://thelicenseurl/"),
                    projectUrl: new Uri("http://theprojecturl/"),
                    iconUrl: new Uri("http://theiconurl/"),
                    licenseExpression: licenseExpressionText);
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal(licenseExpressionText, package.LicenseExpression);
                Assert.Equal(EmbeddedLicenseFileType.Absent, package.EmbeddedLicenseType);
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

            [Fact]
            private async Task WillThrowWhenThePackageRegistrationAndVersionAlreadyExists()
            {
                var currentUser = new User();
                var packageId = "theId";
                var packageVersion = "1.0.32";
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(packageId, packageVersion);
                var packageRegistration = new PackageRegistration
                {
                    Id = packageId,
                    Owners = new HashSet<User> { currentUser },
                };
                packageRegistration.Packages.Add(new Package() { Version = packageVersion });
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration); });

                await Assert.ThrowsAsync<PackageAlreadyExistsException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false));
            }

            [Fact]
            private async Task WillThrowIfTheNuGetPackageIdIsLongerThanMaxPackageIdLength()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId".PadRight(131, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ID", NuGet.Services.Entities.Constants.MaxPackageIdLength), ex.Message);
            }

            [Fact]
            private async Task DoesNotThrowIfTheNuGetPackageSpecialVersionContainsADot()
            {
                var user = new User();
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId", version: "1.2.3-alpha.0");

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: user, currentUser: user, isVerified: false);
            }

            [Fact]
            private async Task DoesNotThrowIfTheNuGetPackageSpecialVersionContainsOnlyNumbers()
            {
                var user = new User();
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId", version: "1.2.3-12345");

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: user, currentUser: user, isVerified: false);
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
            private async Task WillThrowIfTheNuGetPackageReleaseNotesIsLongerThan35000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(releaseNotes: "theReleaseNotes".PadRight(35001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ReleaseNotes", "35000"), ex.Message);
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

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependency.Id", NuGet.Services.Entities.Constants.MaxPackageIdLength), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfThePackageContainsDuplicateDependencyGroups()
            {
                var service = CreateService();

                var duplicateDependencyGroup = new PackageDependencyGroup(
                        new NuGetFramework("net40"),
                        new[]
                        {
                            new NuGet.Packaging.Core.PackageDependency(
                                "dependency",
                                VersionRange.Parse("[1.0.0, 2.0.0)")),
                        });

                var packageDependencyGroups = new[]
                {
                    duplicateDependencyGroup,
                    new PackageDependencyGroup(
                        new NuGetFramework("net35"),
                        new[]
                        {
                            new NuGet.Packaging.Core.PackageDependency(
                                "dependency",
                                VersionRange.Parse("[1.0]"))
                        }),
                    duplicateDependencyGroup
                };

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(packageDependencyGroups: packageDependencyGroups);

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(ServicesStrings.NuGetPackageDuplicateDependencyGroup, ex.Message);
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

            [Fact]
            private async Task WillThrowIfTheRepositoryTypeIsLongerThan100()
            {
                // Arrange
                var service = CreateService();

                var repositoryMetadata = new NuGet.Packaging.Core.RepositoryMetadata()
                {
                    Type = new string('a', 101),
                    Url = "https://github.com/NuGet/NuGetGallery",
                };

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(repositoryMetadata: repositoryMetadata);

                // Act
                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                // Assert
                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "RepositoryType", "100"), ex.Message);
            }

            [Fact]
            private async Task WillThrowIfTheRepositoryUrlIsLongerThan4000()
            {
                // Arrange
                var service = CreateService();

                var repositoryMetadata = new NuGet.Packaging.Core.RepositoryMetadata()
                {
                    Type = "git",
                    Url = "https://github.com/NuGet/NuGetGallery" + new string('a', 4000),
                };

                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(repositoryMetadata: repositoryMetadata);

                // Act
                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                // Assert
                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "RepositoryUrl", "4000"), ex.Message);
            }
        }

        public class TheFilterExactPackageMethod
        {
            [Fact]
            public void ThrowsIfPackagesNull()
            {
                Assert.Throws<ArgumentNullException>(() => InvokeMethod(null, null));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("1.0.0")]
            public void ReturnsNullIfEmptyList(string version)
            {
                Assert.Equal(null, InvokeMethod(new Package[0], version));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("1.0.0-afakepackage")]
            public void ReturnsNullIfMissing(string version)
            {
                var packages = new[]
                {
                    CreateTestPackage("1.0.0"),
                    CreateTestPackage("2.0.0")
                };

                Assert.Equal(null, InvokeMethod(packages, version));
            }

            /// <remarks>
            /// The method should compare the normalized version of the package and be case-insensitive.
            /// </remarks>
            [Theory]
            [InlineData("1.0.0-a")]
            [InlineData("1.0.0-A")]
            [InlineData("1.0.0-a+metadata")]
            [InlineData("1.0.0-A+metadata")]
            public void ReturnsVersionIfExists(string version)
            {
                var package = CreateTestPackage("1.0.0-a");
                var packages = new[] 
                {
                    package,
                    CreateTestPackage("2.0.0")
                };

                Assert.Equal(package, InvokeMethod(packages, version));
            }

            private Package InvokeMethod(IReadOnlyCollection<Package> packages, string version)
            {
                var service = CreateService();
                return service.FilterExactPackage(packages, version);
            }

            private Package CreateTestPackage(string normalizedVersion)
            {
                return new Package
                {
                    NormalizedVersion = normalizedVersion
                };
            }
        }

        public class TheFilterLatestPackageMethod
        {
            protected const string Id = "theId";

            [Theory]
            [InlineData(null)]
            [InlineData("2.0.0")]
            public void ReturnsTheLatestStableVersionIfAvailable(string semVerLevel)
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = Id };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true, IsLatestStableSemVer2 = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };

                // Act
                var result = InvokeMethod(new[] { package1, package2 }, semVerLevelKey: SemVerLevelKey.ForSemVerLevel(semVerLevel));

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0", result.Version);
            }

            [Fact]
            public void ReturnsTheLatestStableSemVer2VersionIfAvailable()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = Id };
                var package0 = new Package { Version = "1.0.0+metadata", PackageRegistration = packageRegistration, Listed = true, IsLatestStableSemVer2 = true };
                var package1 = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = true, IsLatestStable = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };

                // Act
                var result = InvokeMethod(new[] { package0, package1, package2 }, semVerLevelKey: SemVerLevelKey.SemVer2);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0.0+metadata", result.Version);
            }

            [Fact]
            public void ReturnsTheLatestVersionIfNoLatestStableVersionIsAvailable()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = Id };
                var package1 = new Package { Version = "1.0.0b", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true };

                // Act
                var result = InvokeMethod(new[] { package1, package2 });

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0.0b", result.Version);
            }

            [Fact]
            public void ReturnsNullIfNoLatestStableVersionIsAvailableAndPrereleaseIsDisallowed()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = Id };
                var package1 = new Package { Version = "1.0.0b", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true, IsLatest = true };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = true };

                // Act
                var result = InvokeMethod(new[] { package1, package2 }, allowPrerelease: false);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ReturnsTheMostRecentVersionIfNoLatestVersionIsAvailable()
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = Id };
                var package1 = new Package { Version = "1.0.0b", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = false };
                var package2 = new Package { Version = "1.0.0a", PackageRegistration = packageRegistration, IsPrerelease = true, Listed = false };

                // Act
                var result = InvokeMethod(new[] { package1, package2 });

                // Assert
                Assert.NotNull(result);
                Assert.Equal("1.0.0b", result.Version);
            }

            [Fact]
            public void ThrowsIfPackagesNull()
            {
                Assert.Throws<ArgumentNullException>(() => InvokeMethod(null));
            }

            protected virtual Package InvokeMethod(
                IReadOnlyCollection<Package> packages,
                int? semVerLevelKey = SemVerLevelKey.SemVer2,
                bool allowPrerelease = true)
            {
                return CreateService().FilterLatestPackage(packages, semVerLevelKey, allowPrerelease);
            }
        }

        public class TheFindPackageByIdAndVersionMethod : TheFilterLatestPackageMethod
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

                service.FindPackageByIdAndVersion(Id, "1.0.42");

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

            protected override Package InvokeMethod(
                IReadOnlyCollection<Package> packages, 
                int? semVerLevelKey = 2, 
                bool allowPrerelease = true)
            {
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                repository
                    .Setup(repo => repo.GetAll())
                    .Returns(packages.AsQueryable());
                var service = CreateService(packageRepository: repository);
                return service.FindPackageByIdAndVersion(Id, null, semVerLevelKey, allowPrerelease);
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
            public override void ReturnsCorrectLatestVersionForMixedSemVer2AndNonSemVer2PackageVersions_IncludeUnlistedTrue(User currentUser, User packageOwner)
                => base.ReturnsCorrectLatestVersionForMixedSemVer2AndNonSemVer2PackageVersions_IncludeUnlistedTrue(currentUser, packageOwner);

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
            public override void ReturnsCorrectLatestVersionForMixedSemVer2AndNonSemVer2PackageVersions_IncludeUnlistedTrue(User currentUser, User packageOwner)
                => base.ReturnsCorrectLatestVersionForMixedSemVer2AndNonSemVer2PackageVersions_IncludeUnlistedTrue(currentUser, packageOwner);

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
                Assert.Single(packages);
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
                Assert.Empty(packages);
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
                Assert.Single(packages);
            }

            [Theory]
            public virtual void ReturnsAPackageForEachPackageRegistration(User currentUser, User packageOwner)
            {
                var packageRegistrationA = new PackageRegistration { Key = 0, Id = "idA", Owners = { packageOwner } };
                var packageRegistrationB = new PackageRegistration { Key = 1, Id = "idB", Owners = { packageOwner } };
                var packageA = new Package
                {
                    Version = "1.0",
                    PackageRegistration = packageRegistrationA,
                    PackageRegistrationKey = 0,
                    Listed = true,
                    IsLatestSemVer2 = true,
                    IsLatestStableSemVer2 = true
                };
                var packageB = new Package
                {
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
                Assert.Single(packages);
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
                Assert.Single(packages);
                Assert.Contains(latestStablePackage, packages);
            }

            [Theory]
            public virtual void ReturnsCorrectLatestVersionForMixedSemVer2AndNonSemVer2PackageVersions_IncludeUnlistedTrue(User currentUser, User packageOwner)
            {
                var context = GetMixedVersioningPackagesContext(currentUser, packageOwner);

                var packages = InvokeFindPackagesByOwner(currentUser, includeUnlisted: true).ToList();

                var nugetCatalogReaderPackage = packages.Single(p => p.PackageRegistration.Id == "NuGet.CatalogReader");
                Assert.Equal("1.5.12+git.78e44a8", NuGetVersionFormatter.ToFullString(nugetCatalogReaderPackage.Version));

                var sleetLibPackage = packages.Single(p => p.PackageRegistration.Id == "SleetLib");
                Assert.Equal("2.2.24+git.f2a0cb6", NuGetVersionFormatter.ToFullString(sleetLibPackage.Version));
            }

            protected FakeEntitiesContext GetMixedVersioningPackagesContext(User currentUser, User packageOwner)
            {
                var context = GetFakeContext();

                context.Users.Add(currentUser);

                var sleetLibRegistration = new PackageRegistration { Key = 0, Id = "SleetLib", Owners = { packageOwner } };
                var sleetLibPackages = new[]
                {
                    new Package { PackageRegistrationKey = 0, Version = "2.2.24+git.f2a0cb6", PackageRegistration = sleetLibRegistration, Listed = true, IsLatestStableSemVer2 = true, IsLatestSemVer2 = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.18+git.4d361d8", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.16+git.c6be4b4", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.13+git.e657e80", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.9+git.4a81f0c", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.7+git.393c301", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.3+git.98f8237", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.1+git.e11393a", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.2.0+git.6973dc7", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.0.0+git.5106315", PackageRegistration = sleetLibRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 0, Version = "2.0.0-beta.19+git.hash.befdb81dbbef6fb5b8cdf147cc467f9904339cc8", PackageRegistration = sleetLibRegistration, Listed = false },
                    new Package { PackageRegistrationKey = 0, Version = "1.1.0-beta-296", PackageRegistration = sleetLibRegistration, Listed = true, IsLatest = true }
                };
                context.PackageRegistrations.Add(sleetLibRegistration);
                foreach (var package in sleetLibPackages)
                {
                    context.Packages.Add(package);
                }

                var nugetCatalogReaderRegistration = new PackageRegistration { Key = 1, Id = "NuGet.CatalogReader", Owners = { packageOwner } };
                var nugetCatalogReaderPackages = new[]
                {
                    new Package { PackageRegistrationKey = 1, Version = "1.5.12+git.78e44a8", PackageRegistration = nugetCatalogReaderRegistration, Listed = true, IsLatestStableSemVer2 = true, IsLatestSemVer2 = true },
                    new Package { PackageRegistrationKey = 1, Version = "1.5.8+git.bcda3b8", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 1, Version = "1.4.0+git.e2a36b6", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 1, Version = "1.3.0+git.a6a89a3", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 1, Version = "1.2.0", PackageRegistration = nugetCatalogReaderRegistration, Listed = true, IsLatest = true, IsLatestStable = true },
                    new Package { PackageRegistrationKey = 1, Version = "1.1.0", PackageRegistration = nugetCatalogReaderRegistration, Listed = true },
                    new Package { PackageRegistrationKey = 1, Version = "1.0.0", PackageRegistration = nugetCatalogReaderRegistration, Listed = true }
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
                Assert.Single(packages);
                Assert.Contains(package2, packages);
            }

            [Theory]
            public virtual void ReturnsVersionsWhenIncludedVersionsIsTrue_IncludeUnlistedTrue(User currentUser, User packageOwner)
            {
                var packageRegistration = new PackageRegistration { Key = 0, Id = "theId", Owners = { packageOwner } };

                var package1 = new Package
                {
                    Version = "1.0",
                    PackageRegistration = packageRegistration,
                    PackageRegistrationKey = 0,
                    Listed = false,
                    IsLatest = false,
                    IsLatestStable = false
                };
                packageRegistration.Packages.Add(package1);

                var package2 = new Package
                {
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
                Assert.Single(packages);
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

                Assert.NotEqual(default(DateTime), package.Published);
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

                Assert.NotEqual(default(DateTime), package.Published);
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

        public class TheEnsureValidMethod
        {
            [Fact]
            public async Task EnsureValidThrowsForSymbolsPackage()
            {
                // Arrange
                var service = CreateService();
                var packageStream = TestPackage.CreateTestSymbolPackageStream();
                var packageArchiveReader = PackageServiceUtility.CreateArchiveReader(packageStream);

                // Act and Assert
                await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.EnsureValid(packageArchiveReader));
            }
        }

        public class TheRemovePackageOwnerMethod
        {
            [Fact]
            public async Task WillRemoveOneOfManyOwners()
            {
                var service = CreateService();
                var owner1 = new User { Key = 1, Username = "Owner1" };
                var owner2 = new User { Key = 2, Username = "Owner2" };
                var package = new PackageRegistration { Owners = new List<User> { owner1, owner2 } };

                await service.RemovePackageOwnerAsync(package, owner1);

                Assert.DoesNotContain(owner1, package.Owners);
            }

            [Fact]
            public async Task WillRemoveLastOwner()
            {
                var service = CreateService();
                var singleOwner = new User { Key = 1, Username = "Owner" };
                var package = new PackageRegistration { Owners = new List<User> { singleOwner } };

                await service.RemovePackageOwnerAsync(package, singleOwner);

                Assert.DoesNotContain(singleOwner, package.Owners);
            }
        }

        public class TheWillOrphanPackageIfOwnerRemovedMethod
        {
            [Flags]
            public enum OwnershipState
            {
                OwnedByUser1 = 1 << 0,
                OwnedByUser2 = 1 << 1,
                OwnedByOrganization1 = 1 << 2,
                OwnedByOrganization2 = 1 << 3,
                User1InOrganization1 = 1 << 4,
                User2InOrganization1 = 1 << 5,
                User1InOrganization2 = 1 << 6,
                User2InOrganization2 = 1 << 7,
            }

            public enum AccountToDelete
            {
                User1,
                User2,
                Organization1,
                Organization2
            }

            public static IEnumerable<object[]> WillBeOrphaned_Input
            {
                get
                {
                    for (int i = 0; i < Enum.GetValues(typeof(OwnershipState)).Cast<int>().Max() * 2; i++)
                    {
                        var ownershipState = (OwnershipState)i;
                        foreach (var accountToDelete in Enum.GetValues(typeof(AccountToDelete)).Cast<AccountToDelete>())
                        {
                            yield return new object[] { ownershipState, accountToDelete };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WillBeOrphaned_Input))]
            public void WillBeOrphaned(OwnershipState state, AccountToDelete accountToDelete)
            {
                // Create users to test
                var user1 = new User("testUser1") { Key = 0 };
                var user2 = new User("testUser2") { Key = 1 };
                var organization1 = new Organization("testOrganization1") { Key = 2 };
                var organization2 = new Organization("testOrganization2") { Key = 3 };

                // Configure organization membership
                if (state.HasFlag(OwnershipState.User1InOrganization1))
                {
                    AddMemberToOrganization(organization1, user1);
                }

                if (state.HasFlag(OwnershipState.User2InOrganization1))
                {
                    AddMemberToOrganization(organization1, user2);
                }

                if (state.HasFlag(OwnershipState.User1InOrganization2))
                {
                    AddMemberToOrganization(organization2, user1);
                }

                if (state.HasFlag(OwnershipState.User2InOrganization2))
                {
                    AddMemberToOrganization(organization2, user2);
                }

                // Configure package ownership
                var package = new Package();
                var packageRegistration = new PackageRegistration() { Key = 4 };
                packageRegistration.Packages.Add(package);

                if (state.HasFlag(OwnershipState.OwnedByUser1))
                {
                    packageRegistration.Owners.Add(user1);
                }

                if (state.HasFlag(OwnershipState.OwnedByUser2))
                {
                    packageRegistration.Owners.Add(user2);
                }

                if (state.HasFlag(OwnershipState.OwnedByOrganization1))
                {
                    packageRegistration.Owners.Add(organization1);
                }

                if (state.HasFlag(OwnershipState.OwnedByOrganization2))
                {
                    packageRegistration.Owners.Add(organization2);
                }

                // Determine expected result and account to delete
                var expectedResult = true;
                User userToDelete;
                if (accountToDelete == AccountToDelete.User1)
                {
                    userToDelete = user1;

                    // If we delete the first user, the package is orphaned unless it is owned by the second user or an organization that the second user is a member of.
                    if (state.HasFlag(OwnershipState.OwnedByUser2) ||
                        (state.HasFlag(OwnershipState.OwnedByOrganization1) && state.HasFlag(OwnershipState.User2InOrganization1)) ||
                        (state.HasFlag(OwnershipState.OwnedByOrganization2) && state.HasFlag(OwnershipState.User2InOrganization2)))
                    {
                        expectedResult = false;
                    }
                }
                else if (accountToDelete == AccountToDelete.User2)
                {
                    userToDelete = user2;

                    // If we delete the second user, the package is orphaned unless it is owned by the first user or an organization that the second user is a member of.
                    if (state.HasFlag(OwnershipState.OwnedByUser1) ||
                        (state.HasFlag(OwnershipState.OwnedByOrganization1) && state.HasFlag(OwnershipState.User1InOrganization1)) ||
                        (state.HasFlag(OwnershipState.OwnedByOrganization2) && state.HasFlag(OwnershipState.User1InOrganization2)))
                    {
                        expectedResult = false;
                    }
                }
                else if (accountToDelete == AccountToDelete.Organization1)
                {
                    userToDelete = organization1;

                    // If we delete the first organization, the package is orphaned unless is it owned a user or it is owned by the second organization and that organization has members.
                    if (state.HasFlag(OwnershipState.OwnedByUser1) ||
                        state.HasFlag(OwnershipState.OwnedByUser2) ||
                        (state.HasFlag(OwnershipState.OwnedByOrganization2) && (state.HasFlag(OwnershipState.User1InOrganization2) || state.HasFlag(OwnershipState.User2InOrganization2))))
                    {
                        expectedResult = false;
                    }
                }
                else if (accountToDelete == AccountToDelete.Organization2)
                {
                    userToDelete = organization2;

                    // If we delete the second organization, the package is orphaned unless is it owned a user or it is owned by the first organization and that organization has members.
                    if (state.HasFlag(OwnershipState.OwnedByUser1) ||
                        state.HasFlag(OwnershipState.OwnedByUser2) ||
                        (state.HasFlag(OwnershipState.OwnedByOrganization1) && (state.HasFlag(OwnershipState.User1InOrganization1) || state.HasFlag(OwnershipState.User2InOrganization1))))
                    {
                        expectedResult = false;
                    }
                }
                else
                {
                    throw new ArgumentException(nameof(accountToDelete));
                }

                // Delete account
                var service = CreateService();
                var result = service.WillPackageBeOrphanedIfOwnerRemoved(packageRegistration, userToDelete);

                // Assert expected result
                Assert.Equal(expectedResult, result);
            }

            [Fact]
            public void APackageIdThatHasOnlyARegistrationCannotBeOrphaned()
            {
                // Create users to test
                var user = new User("testUser") { Key = 0 };
               
                // Configure package registration ownership
                var packageRegistration = new PackageRegistration() { Key = 1 };
                packageRegistration.Owners.Add(user);

                // Delete account
                var service = CreateService();
                var result = service.WillPackageBeOrphanedIfOwnerRemoved(packageRegistration, user);

                // Assert expected result
                Assert.False(result);
            }

            private void AddMemberToOrganization(Organization organization, User member)
            {
                var membership = new Membership() { Member = member, Organization = organization };
                organization.Members.Add(membership);
                member.Organizations.Add(membership);
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

        public class TheSetRequiredSignerAsyncMethodOneParameter : TestContainer
        {
            private readonly User _user1;

            public TheSetRequiredSignerAsyncMethodOneParameter()
            {
                _user1 = new User()
                {
                    Key = 1,
                    Username = "a"
                };
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenSignerIsNull_Throws()
            {
                var service = Get<PackageService>();
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.SetRequiredSignerAsync(signer: null));

                Assert.Equal("signer", exception.ParamName);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenNoPackageRegistrationsOwned_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var service = Get<PackageService>();

                await service.SetRequiredSignerAsync(_user1);

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.IsAny<PackageRegistrationAuditRecord>()), Times.Never);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenMultiplePackageRegistrationsOwned_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var entityContext = new FakeEntitiesContext();
                var service = Get<PackageService>();

                var packageRegistration1 = new PackageRegistration()
                {
                    Key = 2,
                    Id = "b"
                };
                var packageRegistration2 = new PackageRegistration()
                {
                    Key = 3,
                    Id = "c"
                };
                var packageRegistration3 = new PackageRegistration()
                {
                    Key = 4,
                    Id = "d"
                };

                var user2 = new User()
                {
                    Key = 5,
                    Username = "e"
                };

                packageRegistration1.Owners.Add(_user1);
                packageRegistration1.Owners.Add(user2);
                packageRegistration1.RequiredSigners.Add(user2);

                packageRegistration2.Owners.Add(_user1);
                packageRegistration2.Owners.Add(user2);

                packageRegistration3.Owners.Add(user2);
                packageRegistration3.Owners.Add(_user1);
                packageRegistration3.RequiredSigners.Add(_user1);

                var packageRegistrations = new[] { packageRegistration1, packageRegistration2, packageRegistration3 };

                foreach (var packageRegistration in packageRegistrations)
                {
                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                packageRegistrationRepository.Setup(x => x.GetAll())
                    .Returns(entityContext.PackageRegistrations);

                await service.SetRequiredSignerAsync(_user1);

                foreach (var packageRegistration in packageRegistrations)
                {
                    Assert.Equal(1, packageRegistration.RequiredSigners.Count);
                    Assert.Equal(_user1, packageRegistration.RequiredSigners.Single());
                }

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.Is<PackageRegistrationAuditRecord>(
                        record =>
                            record.Action == AuditedPackageRegistrationAction.SetRequiredSigner &&
                            record.Id == packageRegistration1.Id &&
                            record.PreviousRequiredSigner == user2.Username &&
                            record.NewRequiredSigner == _user1.Username)), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.Is<PackageRegistrationAuditRecord>(
                        record =>
                            record.Action == AuditedPackageRegistrationAction.SetRequiredSigner &&
                            record.Id == packageRegistration2.Id &&
                            record.PreviousRequiredSigner == null &&
                            record.NewRequiredSigner == _user1.Username)), Times.Once);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(
                    It.Is<string>(packageId => packageId == packageRegistration1.Id)), Times.Once);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(
                    It.Is<string>(packageId => packageId == packageRegistration2.Id)), Times.Once);
            }
        }

        public class TheSetRequiredSignerAsyncMethodTwoParameters : TestContainer
        {
            private readonly PackageRegistration _packageRegistration;
            private readonly User _user1;
            private readonly User _user2;

            public TheSetRequiredSignerAsyncMethodTwoParameters()
            {
                _packageRegistration = new PackageRegistration()
                {
                    Key = 1,
                    Id = "a"
                };

                _user1 = new User()
                {
                    Key = 2,
                    Username = "b"
                };

                _user2 = new User()
                {
                    Key = 3,
                    Username = "c"
                };
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenRegistrationIsNull_Throws()
            {
                var service = Get<PackageService>();
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => service.SetRequiredSignerAsync(registration: null, signer: _user1));

                Assert.Equal("registration", exception.ParamName);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenCurrentRequiredSignerIsNullAndNewRequiredSignerIsNull_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var service = Get<PackageService>();

                await service.SetRequiredSignerAsync(_packageRegistration, signer: null);

                Assert.Empty(_packageRegistration.RequiredSigners);

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.IsAny<PackageRegistrationAuditRecord>()), Times.Never);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenCurrentRequiredSignerIsNullAndNewRequiredSignerIsNotNull_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var entityContext = new FakeEntitiesContext();
                var service = Get<PackageService>();

                entityContext.PackageRegistrations.Add(_packageRegistration);

                packageRegistrationRepository.Setup(x => x.GetAll())
                    .Returns(entityContext.PackageRegistrations);

                await service.SetRequiredSignerAsync(_packageRegistration, _user1);

                Assert.Equal(1, _packageRegistration.RequiredSigners.Count);
                Assert.Equal(_user1, _packageRegistration.RequiredSigners.Single());

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.Is<PackageRegistrationAuditRecord>(
                        record =>
                            record.Action == AuditedPackageRegistrationAction.SetRequiredSigner &&
                            record.Id == _packageRegistration.Id &&
                            record.PreviousRequiredSigner == null &&
                            record.NewRequiredSigner == _user1.Username)), Times.Once);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(
                    It.Is<string>(packageId => packageId == _packageRegistration.Id)), Times.Once);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenCurrentRequiredSignerIsNotNullAndNewRequiredSignerIsNotNull_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var entityContext = new FakeEntitiesContext();
                var service = Get<PackageService>();

                _packageRegistration.RequiredSigners.Add(_user1);

                entityContext.PackageRegistrations.Add(_packageRegistration);

                packageRegistrationRepository.Setup(x => x.GetAll())
                    .Returns(entityContext.PackageRegistrations);

                await service.SetRequiredSignerAsync(_packageRegistration, _user2);

                Assert.Equal(1, _packageRegistration.RequiredSigners.Count);
                Assert.Equal(_user2, _packageRegistration.RequiredSigners.Single());

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.Is<PackageRegistrationAuditRecord>(
                        record =>
                            record.Action == AuditedPackageRegistrationAction.SetRequiredSigner &&
                            record.Id == _packageRegistration.Id &&
                            record.PreviousRequiredSigner == _user1.Username &&
                            record.NewRequiredSigner == _user2.Username)), Times.Once);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(
                    It.Is<string>(packageId => packageId == _packageRegistration.Id)), Times.Once);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenCurrentRequiredSignerIsNotNullAndNewRequiredSignerIsNull_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var entityContext = new FakeEntitiesContext();
                var service = Get<PackageService>();

                _packageRegistration.RequiredSigners.Add(_user1);

                entityContext.PackageRegistrations.Add(_packageRegistration);

                packageRegistrationRepository.Setup(x => x.GetAll())
                    .Returns(entityContext.PackageRegistrations);

                await service.SetRequiredSignerAsync(_packageRegistration, signer: null);

                Assert.Empty(_packageRegistration.RequiredSigners);

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Once);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.Is<PackageRegistrationAuditRecord>(
                        record =>
                            record.Action == AuditedPackageRegistrationAction.SetRequiredSigner &&
                            record.Id == _packageRegistration.Id &&
                            record.PreviousRequiredSigner == _user1.Username &&
                            record.NewRequiredSigner == null)), Times.Once);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(
                    It.Is<string>(packageId => packageId == _packageRegistration.Id)), Times.Once);
            }

            [Fact]
            public async Task SetRequiredSignerAsync_WhenCurrentRequiredSignerAndNewRequiredSignerAreSame_Succeeds()
            {
                var packageRegistrationRepository = GetMock<IEntityRepository<PackageRegistration>>();
                var auditingService = GetMock<IAuditingService>();
                var telemetryService = GetMock<ITelemetryService>();
                var service = Get<PackageService>();

                _packageRegistration.RequiredSigners.Add(_user1);

                await service.SetRequiredSignerAsync(_packageRegistration, _user1);

                Assert.Equal(1, _packageRegistration.RequiredSigners.Count);
                Assert.Equal(_user1, _packageRegistration.RequiredSigners.Single());

                packageRegistrationRepository.Verify(x => x.CommitChangesAsync(), Times.Never);
                auditingService.Verify(x => x.SaveAuditRecordAsync(
                    It.IsAny<PackageRegistrationAuditRecord>()), Times.Never);
                telemetryService.Verify(x => x.TrackRequiredSignerSet(It.IsAny<string>()), Times.Never);
            }
        }

        public class TheEnrichPackageFromNuGetPackageMethod
        {
            [Theory]
            [InlineData("iconfilename", true)]
            [InlineData(null, false)]
            public void SetsEmbeddedIconFlagProperly(string iconFilename, bool expectedFlag)
            {
                var service = CreateService();
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "SomePackage"
                    },
                    HasEmbeddedIcon = false,
                };

                // the EnrichPackageFromNuGetPackage method does not read icon filename from the PackageArchiveReader
                // so we won't bother setting it up here.
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(package.Id);

                var packageArchiveReader = new PackageArchiveReader(packageStream);

                var metadataDictionary = new Dictionary<string, string>
                {
                    { "version", "1.2.3" },
                };

                if (iconFilename != null)
                {
                    metadataDictionary.Add("icon", iconFilename);
                }

                var packageMetadata = new PackageMetadata(
                    metadataDictionary,
                    Enumerable.Empty<PackageDependencyGroup>(),
                    Enumerable.Empty<FrameworkSpecificGroup>(),
                    Enumerable.Empty<NuGet.Packaging.Core.PackageType>(),
                    new NuGetVersion(3, 2, 1),
                    repositoryMetadata: null,
                    licenseMetadata: null);

                service.EnrichPackageFromNuGetPackage(package, packageArchiveReader, packageMetadata, new PackageStreamMetadata(), new User());

                Assert.Equal(expectedFlag, package.HasEmbeddedIcon);
            }
        }
    }
}