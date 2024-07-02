// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.Services;
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
            Action<Mock<PackageService>> setup = null,
            Mock<IEntitiesContext> context = null,
            Mock<IContentObjectService> contentObjectService = null,
            Mock<IFeatureFlagService> featureFlagService = null)
        {
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            certificateRepository = certificateRepository ?? new Mock<IEntityRepository<Certificate>>();
            auditingService = auditingService ?? new TestAuditingService();
            telemetryService = telemetryService ?? new Mock<ITelemetryService>();
            securityPolicyService = securityPolicyService ?? new Mock<ISecurityPolicyService>();
            context = context ?? new Mock<IEntitiesContext>();

            if (contentObjectService == null)
            {
                contentObjectService = new Mock<IContentObjectService>();
                contentObjectService.Setup(x => x.QueryHintConfiguration).Returns(Mock.Of<IQueryHintConfiguration>());
            }

            if (featureFlagService == null)
            {
                featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService.Setup(x => x.ArePatternSetTfmHeuristicsEnabled()).Returns(true);
            }

            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                certificateRepository.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object,
                context.Object,
                contentObjectService.Object,
                featureFlagService.Object);

            packageService.CallBase = true;

            if (setup != null)
            {
                setup(packageService);
            }

            return packageService.Object;
        }
        
        public class TheFindPackageBySuffixMethod
        {
            private Package InvokeMethod(IReadOnlyCollection<Package> packages, string version, bool preRelease)
            {
                var service = CreateService();
                return service.FilterLatestPackageBySuffix(packages, version, preRelease);
            }
            
            [Theory]
            [InlineData("alpha", true, 4)]
            [InlineData("alpha2-internal", true, 4)]
            [InlineData("alpha1", true, 2)]
            [InlineData("alpha2", true, 4)]
            [InlineData("alpha3", true, 7)]
            [InlineData("internal", true, 7)]
            [InlineData("internal.5", true, 7)]
            [InlineData("internal.51", true, 7)]
            [InlineData("internal.6", true, 8)]
            [InlineData("", true, 7)]
            [InlineData("", false, 1)]
            [InlineData("noexist", true, 7)]
            public void VerifySemVerMatching(string version, bool preRelease, int expectedResultIndex)
            {
                var r = new Regex(@"-[^d]");
                var testData = new[]
                    {
                        ("1.0.0", false, false),
                        ("1.0.23", true, false),
                        ("1.0.23-alpha1", false, false),
                        ("1.0.23-alpha2-internal2", false, false),
                        ("1.0.23-alpha2-internal3", false, false),
                        ("1.0.23-beta", false, false),
                        ("1.0.23-internal.5", false, false),
                        ("1.0.23-internal.510", false, true),
                        ("1.0.23-internal.6", false, false),
                    }
                    .Select(data => new Package() { 
                        IsPrerelease = r.IsMatch(data.Item1), 
                        NormalizedVersion = NuGetVersion.Parse(data.Item1).ToNormalizedString(),
                        IsLatestStableSemVer2 = data.Item2,
                        IsLatestSemVer2 = data.Item3
                    })
                    .ToArray();

                var result = InvokeMethod(testData, version, preRelease);
                Assert.Equal(testData[expectedResultIndex].NormalizedVersion, result.NormalizedVersion);
            }
            
            [Fact]
            public void VerifyFallbackToStableIfNoPrerelease()
            {
                var r = new Regex(@"-[^d]");
                var testData = new[]
                    {
                        ("1.0.0", 1, false, false),
                        ("1.0.23", 2, true, false),
                    }
                    .Select(data => new Package() { 
                        IsPrerelease = r.IsMatch(data.Item1), 
                        NormalizedVersion = SemanticVersion.Parse(data.Item1).ToNormalizedString(),
                        IsLatestStableSemVer2 = data.Item3,
                        IsLatestSemVer2 = data.Item4
                    })
                    .ToArray();

                var result = InvokeMethod(testData, "alpha", true);
                Assert.Equal(testData[1].NormalizedVersion, result.NormalizedVersion);
            }
            
            [Fact]
            public void VerifyDoesNotThrowIfNoPackages()
            {
                var result = InvokeMethod(new Package[]{}, "alpha", true);
                Assert.Equal(null, result);
            }
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
                    licenseFilename: "license.txt",
                    readmeFilename:"readme.md");
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal("theId", package.Id);
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
                Assert.Equal(EmbeddedReadmeFileType.Markdown, package.EmbeddedReadmeType);
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

            [Theory]
            [InlineData(null, EmbeddedReadmeFileType.Absent)]
            [InlineData("readme.md", EmbeddedReadmeFileType.Markdown)]
            [InlineData("readme.mD", EmbeddedReadmeFileType.Markdown)]
            public async Task WillDetectReadmeFileType(string readmeFileName, EmbeddedReadmeFileType expectedFileType)
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup:
                        mockPackageService => { mockPackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null); });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(
                    licenseUrl: new Uri("http://thelicenseurl/"),
                    projectUrl: new Uri("http://theprojecturl/"),
                    iconUrl: new Uri("http://theiconurl/"),
                    readmeFilename: readmeFileName);
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal(expectedFileType, package.EmbeddedReadmeType);
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
            public async Task WillSaveTheCreatedPackageWhenANewPackageRegistrationIsCreated()
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
            public async Task WillSaveTheCreatedPackageWhenThePackageRegistrationAlreadyExisted()
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
            public async Task WillThrowWhenThePackageRegistrationAndVersionAlreadyExists()
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
            public async Task WillThrowIfTheNuGetPackageIdIsLongerThanMaxPackageIdLength()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId".PadRight(131, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ID", NuGet.Services.Entities.Constants.MaxPackageIdLength), ex.Message);
            }

            [Fact]
            public async Task DoesNotThrowIfTheNuGetPackageSpecialVersionContainsADot()
            {
                var user = new User();
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId", version: "1.2.3-alpha.0");

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: user, currentUser: user, isVerified: false);
            }

            [Fact]
            public async Task DoesNotThrowIfTheNuGetPackageSpecialVersionContainsOnlyNumbers()
            {
                var user = new User();
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: "theId", version: "1.2.3-12345");

                await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: user, currentUser: user, isVerified: false);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageAuthorsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(authors: "theFirstAuthor".PadRight(2001, '_') + ", " + "theSecondAuthor".PadRight(2001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Authors", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageCopyrightIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(copyright: "theCopyright".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageReleaseNotesIsLongerThan35000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(releaseNotes: "theReleaseNotes".PadRight(35001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ReleaseNotes", "35000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheVersionIsLongerThan64Characters()
            {
                var service = CreateService();
                var versionString = "1.0.0-".PadRight(65, 'a');
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(version: versionString);

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Version", "64"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageDependenciesIsLongerThanInt16MaxValue()
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
            public async Task WillThrowIfThePackageDependencyIdIsLongerThanMaxPackageIdLength()
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
            public async Task WillThrowIfThePackageContainsDuplicateDependencyGroups()
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
            public async Task WillThrowIfThePackageDependencyVersionSpecIsLongerThan256()
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
            public async Task WillThrowIfTheNuGetPackageDescriptionIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(description: "theDescription".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Description", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageIconUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(iconUrl: new Uri("http://theIconUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageLicenseUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(licenseUrl: new Uri("http://theLicenseUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageProjectUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(projectUrl: new Uri("http://theProjectUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageSummaryIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(summary: "theSummary".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Summary", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageTagsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(tags: "theTags".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Tags", "4000"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageTitleIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(title: "theTitle".PadRight(4001, '_'));

                var ex = await Assert.ThrowsAsync<InvalidPackageException>(async () => await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), owner: null, currentUser: null, isVerified: false));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Title", "256"), ex.Message);
            }

            [Fact]
            public async Task WillThrowIfTheNuGetPackageLanguageIsLongerThan20()
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
            public async Task WillSaveSupportedFrameworks()
            {
                var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(packageRegistrationRepository: packageRegistrationRepository, setup: mockPackageService =>
                {
                    mockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    mockPackageService.Setup(p => p.GetSupportedFrameworks(It.IsAny<PackageArchiveReader>())).Returns(
                        new[]
                        {
                                           NuGetFramework.Parse("net40"),
                                           NuGetFramework.Parse("net35"),
                                           NuGetFramework.Parse("any")
                        });
                });
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage();
                var currentUser = new User();

                var package = await service.CreatePackageAsync(nugetPackage.Object, new PackageStreamMetadata(), currentUser, currentUser, isVerified: false);

                Assert.Equal("net40", package.SupportedFrameworks.First().TargetFramework);
                Assert.Equal("net35", package.SupportedFrameworks.ElementAt(1).TargetFramework);
                Assert.Equal("any", package.SupportedFrameworks.ElementAt(2).TargetFramework);
            }

            [Theory]
            [InlineData(false, new[] { "net40", "net5.0", "netcore21" })]
            [InlineData(true, new[] { "net5.0", "netcore21" })]
            public void UsesTfmHeuristicsBasedOnFeatureFlag(bool useNewTfmHeuristics, IEnumerable<string> expectedSupportedTfms)
            {
                // arrange
                // - create a package that responds differently to each set of heuristics
                var nuspec =
                    @"<?xml version=""1.0""?>
                        <package xmlns = ""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                            <metadata>
                                <id>Foo</id>
                                <frameworkAssemblies>
                                    <frameworkAssembly assemblyName=""System"" targetFramework="".NETFramework4.0"" />
                                </frameworkAssemblies>
                            </metadata>
                        </package>";
                var nuspecReader = new NuspecReader(XDocument.Parse(nuspec));
                var files = new List<string> { "lib/netcore2.1/_._", "lib/net5.0/_._" };
                var package = new MockPackageArchiveReader(nuspecReader, files);

                // - create feature flag services and package services for both scenarios
                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService.Setup(x => x.ArePatternSetTfmHeuristicsEnabled()).Returns(useNewTfmHeuristics);

                // act
                var supportedFrameworks = CreateService(featureFlagService: featureFlagService).GetSupportedFrameworks(package)
                    .Select(f => f.GetShortFolderName())
                    .OrderBy(f => f)
                    .ToList();

                // assert
                Assert.Equal<string>(expectedSupportedTfms, supportedFrameworks);
            }

            [Theory]
            [MemberData(nameof(TargetFrameworkCases))]
            public void DeterminesCorrectSupportedFrameworksFromFileList(bool isTools, List<string> files, List<string> expectedSupportedFrameworks)
            {
                // arrange
                var nuspec = isTools
                    ? @"<?xml version=""1.0""?>
                        <package xmlns = ""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                            <metadata>
                                <id>Foo</id>
                                <packageTypes>
                                    <packageType name=""DotnetTool""/>
                                </packageTypes>
                            </metadata>
                        </package>"
                    : @"<?xml version=""1.0""?>
                        <package xmlns = ""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                            <metadata>
                                <id>Foo</id>
                            </metadata>
                        </package>";
                var nuspecReader = new NuspecReader(XDocument.Parse(nuspec));

                // act
                var supportedFrameworks = CreateService().GetSupportedFrameworks(nuspecReader, files)
                    .Select(f => f.GetShortFolderName())
                    .OrderBy(f => f)
                    .ToList();

                // assert
                Assert.Equal<string>(expectedSupportedFrameworks, supportedFrameworks);
            }

            /// <summary>
            /// These cases use the guidance laid out here:
            /// https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package
            /// https://docs.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks
            /// https://docs.microsoft.com/en-us/nuget/reference/target-frameworks
            /// https://docs.microsoft.com/en-us/dotnet/standard/frameworks
            /// </summary>
            public static IEnumerable<object[]> TargetFrameworkCases =>
                new List<object[]>
                {
                    // Runtimes
                    // - note that without "runtimes/" we don't use runtime ids (RIDs).
                    new object[] {false, new List<string> {"lib/net40/_._", "lib/net45/_._"}, new List<string> {"net40", "net45"}},
                    new object[] {false, new List<string> {"lib/net40/_._", "lib/net471/_._"}, new List<string> {"net40", "net471"}},
                    new object[] {false, new List<string> {"lib/net40/_._", "lib/net4.7.1/_._"}, new List<string> {"net40", "net471"}},
                    new object[] {false, new List<string> {"lib/netcoreapp31/_._", "lib/netstandard20/_._"}, new List<string> {"netcoreapp3.1", "netstandard2.0"}},
                    new object[] {false, new List<string> {"lib/_._"}, new List<string> {"net"}},        // no version
                    new object[] {false, new List<string> {"lib/win/_._"}, new List<string> {"win"}},    // note that this is "win" the TFM (i.e. dep'd/replaced by netcore45), not "win" the RID.
                    new object[] {false, new List<string> {"lib/foo/_._"}, new List<string> {"foo"}},    // this will be generated as a TFM if users use it, but it's meaningless to us
                    new object[] {false, new List<string> {"lib/any/_._"}, new List<string> {"dotnet"}}, // "dotnet" is deprecated but is still discernable through this pattern
                    // - resources
                    new object[] {false, new List<string> {"lib/netcoreapp31/zh-hant/_._", "lib/netstandard20/zh_hant_._"}, new List<string> {"netcoreapp3.1", "netstandard2.0"}},
                    new object[] {false, new List<string> {"lib/netcoreapp31/fr-fr/_._", "lib/netstandard20/zh_hans_._"}, new List<string> {"netcoreapp3.1", "netstandard2.0"}},
                    // - portables
                    new object[] {false, new List<string> {"lib/portable-net45+sl40+win+wp71/_._", "lib/portable-net45+sl50+win+wp71+wp80/_._"},
                        new List<string> {"portable-net45+sl4+win+wp71", "portable-net45+sl5+win+wp71+wp8"}},
                    new object[] {false, new List<string> {"lib/portable-profile14/_._", "lib/portable-profile154/_._", "lib/portable-profile7/_._"},
                        new List<string> {"portable-net40+sl5", "portable-net45+sl4+win8+wp8", "portable-net45+win8"}},
                    new object[] {false, new List<string> {"lib/portable-net45+sl50+foo50+wp71+wp80/_._", "lib/portable-net45+foo40+win+wp71/_._"},
                        new List<string> {"portable-net45+sl5+unsupported+wp71+wp8", "portable-net45+unsupported+win+wp71"}},
                    new object[] {false, new List<string> {"lib/portable-net45+monotouch+monoandroid10+xamarintvos/_._"}, new List<string> { "portable-monoandroid10+monotouch+net45+xamarintvos"}},
                    // - including "runtimes/" gives us the option of runtime ids (RIDs) but these won't affect TFM determination
                    new object[] {false, new List<string> {"runtimes/win/net40/_._", "runtimes/win/net471/_._"}, new List<string>()},                   // no "lib" dir
                    new object[] {false, new List<string> {"runtimes/win/foostuff/net40/_._", "runtimes/win/foostuff/net471/_._"}, new List<string>()}, // no "lib" dir
                    new object[] {false, new List<string> {"runtimes/win/lib/net40/_._", "runtimes/win/lib/net471/_._"}, new List<string> {"net40", "net471"}},
                    new object[] {false, new List<string> {"runtimes/win/lib/net40/", "runtimes/win/lib/net471/_._"}, new List<string> {"net471"}},     // no file in "net40" dir
                    // - resources
                    new object[] {false, new List<string> {"runtimes/win/lib/net471/_._", "runtimes/win/lib/net472/fr-fr/_._"}, new List<string> {"net471", "net472"}},
                    // - supporting different TFMs for different RIDs won't be exposed here--we just want the union set of all supported TFMs
                    new object[] {false, new List<string> {"runtimes/win10-x64/lib/net40/_._", "runtimes/win10-arm/lib/net471/_._"}, new List<string> {"net40", "net471"}},
                    // - net5.0+ runtimes
                    new object[] {false, new List<string> {"lib/net5.0/_._", "lib/net5.0/_._"}, new List<string> {"net5.0"}},
                    new object[] {false, new List<string> {"lib/net5.0-tvos/_._", "lib/net5.0-ios/_._"}, new List<string> {"net5.0-ios", "net5.0-tvos"}},
                    new object[] {false, new List<string> {"lib/net5.0-tvos/_._", "lib/net5.0-ios13.0/_._"}, new List<string> {"net5.0-ios13.0", "net5.0-tvos"}},
                    new object[] {false, new List<string> {"lib/net5.1-tvos/_._", "lib/net5.1/_._", "lib/net5.0-tvos/_._"}, new List<string> {"net5.0-tvos", "net5.1", "net5.1-tvos"}},
                    new object[] {false, new List<string> {"lib/net5.0/_1._", "lib/net5.0/_2._", "lib/native/_._"}, new List<string> {"native", "net5.0" }},

                    // Compile time refs
                    new object[] {false, new List<string> {"ref/_._"}, new List<string>()},
                    new object[] {false, new List<string> {"ref/net40/_._", "ref/net451/_._"}, new List<string> {"net40", "net451"}},
                    new object[] {false, new List<string> {"ref/net5.0-watchos/_1._", "ref/net5.0-watchos/_2._" }, new List<string> {"net5.0-watchos"}},
                    new object[] {false, new List<string> {"ref/net5.0-macos/_1._", "ref/net5.0-windows/_2._" }, new List<string> {"net5.0-macos", "net5.0-windows"}},

                    // Build props/targets
                    // - only if a props or target file is present of the name {id}.props|targets ({id} is "Foo" in our case) will the TFM be supported
                    new object[] {false, new List<string> {"build/net40/Foo.props", "build/net42/Foo.targets"}, new List<string> {"net40", "net42"}},
                    new object[] {false, new List<string> {"build/net40/Bar.props", "build/net42/Foo.targets"}, new List<string> {"net42"}},
                    new object[] {false, new List<string> {"build/net40/Bar.props", "build/net42/Bar.targets"}, new List<string>()},
                    new object[] {false, new List<string> {"build/net5.0/Foo.props", "build/net42/Foo.targets"}, new List<string> {"net42", "net5.0"}},
                    // - "any" is a special case for build, where having no specific TFM is valid
                    new object[] {false, new List<string> {"build/Foo.props", "build/Foo.targets"}, new List<string> {"any"}},
                    new object[] {false, new List<string> {"build/Bar.props", "build/Foo.targets"}, new List<string> {"any"}},
                    new object[] {false, new List<string> {"build/Bar.props", "build/Bar.targets"}, new List<string>()},

                    // Tools
                    // - a special case where we will only assess tools TFMs when the nuspec indicates it is a tools (and only a tools) package - hence the true bool
                    // - also, a file in the TFM root doesn't qualify a TFM-supported tool - an RID or "any" must be provided
                    // - see this: https://github.com/NuGet/Home/issues/6197#issuecomment-349495271 - "any" covers portables.
                    new object[] {true, new List<string> {"tools/netcoreapp3.1/_._"}, new List<string>()},
                    new object[] {true, new List<string> {"tools/netcoreapp3.1/win10-x86/_._"}, new List<string> {"netcoreapp3.1"}},
                    new object[] {true, new List<string> {"tools/netcoreapp3.1/win10-x86/tool1/_._", "tools/netcoreapp3.1/win10-x86/tool2/_._" }, 
                        new List<string> {"netcoreapp3.1"}},
                    new object[] {true, new List<string> {"tools/netcoreapp3.1/any/_._"}, new List<string> {"netcoreapp3.1"}},
                    new object[] {true, new List<string> {"tools/netcoreapp3.1/win/tool1/_._"}, new List<string> {"netcoreapp3.1"}},
                    new object[] {true, new List<string> {"tools/netcoreapp3.1/win/any/_._"}, new List<string> {"netcoreapp3.1"}},
                    new object[] {false, new List<string> {"tools/netcoreapp3.1/any/_._"}, new List<string>()}, // not a tools package, no supported TFMs

                    // Content
                    new object[] {false, new List<string> {"contentFiles/css/_._"}, new List<string>()},
                    new object[] {false, new List<string> {"contentFiles/any/_._"}, new List<string>()},
                    new object[] {false, new List<string> {"contentFiles/cs/netstandard2.0/_._"}, new List<string>{"netstandard2.0"}},
                    new object[] {false, new List<string> {"contentFiles/any/netstandard2.0/_._"}, new List<string>{"netstandard2.0"}},
                    new object[] {false, new List<string> {"contentFiles/cs/any/_._"}, new List<string>{"any"}},
                    new object[] {false, new List<string> {"contentFiles/vb/net45/_._", "contentFiles/cs/netcoreapp3.1/_._"},
                        new List<string>{"net45", "netcoreapp3.1"}},

                    // Combinations
                    new object[] 
                    {
                        false,
                        new List<string>
                        {
                            "Foo.nuspec",
                            "runtimes/win10-x86/lib/net40/_._",
                            "runtimes/win10-x86/lib/net471/_._",
                            "ref/net5.0-watchos/_1._",
                            "ref/net5.0-watchos/_2._",
                            "build/netstandard21/Foo.props",
                            "build/netstandard20/Foo.targets",
                            "tools/netcoreapp3.1/win10-x86/tool1/_._",
                            "tools/netcoreapp3.1/win10-x86/tool2/_._"
                        }, 
                        new List<string>{"net40", "net471", "net5.0-watchos", "netstandard2.0", "netstandard2.1"}
                    },
                    // - note that a tools package (true below) is *only* a tools package when evaluating TFM support
                    new object[] 
                    {
                        true, // tools package
                        new List<string>
                        {
                            "Foo.nuspec",
                            "runtimes/win10-x86/lib/net40/_._",
                            "runtimes/win10-x86/lib/net471/_._",
                            "ref/net5.0-watchos/_1._",
                            "ref/net5.0-watchos/_2._",
                            "build/netstandard21/Foo.props",
                            "build/netstandard20/Foo.targets",
                            "tools/netcoreapp3.1/win10-x86/tool1/_._",
                            "tools/netcoreapp3.1/win10-x86/tool2/_._"
                        }, 
                        new List<string> {"netcoreapp3.1"}
                    },
                    new object[]
                    {
                        false,
                        new List<string>
                        {
                            "Foo.nuspec",
                            "runtimes/win10-x86/lib/xamarinios/_._",
                            "runtimes/win10-x64/lib/xamarinios/_._",
                            "ref/xamarinios/_1._",
                            "ref/xamarinios/_2._",
                            "build/netstandard21/Foo.props",
                            "build/netstandard21/Foo.targets",
                            "contentFiles/vb/net45/_._", 
                            "contentFiles/cs/netstandard2.1/_._"
                        },
                        new List<string>{"net45", "netstandard2.1", "xamarinios"}
                    },
                    new object[]
                    {
                        false, 
                        new List<string>
                        {
                            ".signature.p7s",
                            "LICENSE.md",
                            "Foo.nuspec",
                            "fooIcon.png",
                            "[Content_Types].xml",
                            "_rels/.rels",
                            "package/service/metadata/core-properties/foo1234.psmdcp",
                            "lib/net20/Foo.dll",
                            "lib/net35/Foo.dll",
                            "lib/net40/Foo.dll",
                            "lib/net45/Foo.dll",
                            "lib/netstandard1.0/Foo.dll",
                            "lib/netstandard1.3/Foo.dll",
                            "lib/netstandard2.0/Foo.dll",
                            "lib/portable-net40+sl5+win8+wp8+wpa81/Foo.dll",
                            "lib/portable-net45+win8+wp8+wpa81/Foo.dll"
                        },
                        new List<string> {"net20", "net35", "net40", "net45", "netstandard1.0", "netstandard1.3", "netstandard2.0",
                            "portable-net40+sl5+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"}
                    }
                };

            [Fact]
            public async Task WillThrowIfTheRepositoryTypeIsLongerThan100()
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
            public async Task WillThrowIfTheRepositoryUrlIsLongerThan4000()
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
                Assert.Equal(null, InvokeMethod(Array.Empty<Package>(), version));
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

        public class TheGetPackageDependentsMethod
        {
            [Fact]
            public void AllQueriesShouldUseQueryHint()
            {
                string id = "foo";
                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();
                var disposable = new Mock<IDisposable>();

                var operations = new List<string>();

                disposable
                    .Setup(x => x.Dispose())
                    .Callback(() => operations.Add(nameof(IDisposable.Dispose)));
                context
                    .Setup(x => x.WithQueryHint(It.IsAny<string>()))
                    .Returns(() => disposable.Object)
                    .Callback(() => operations.Add(nameof(EntitiesContext.WithQueryHint)));
                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies)
                    .Callback(() => operations.Add(nameof(EntitiesContext.PackageDependencies)));
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages)
                    .Callback(() => operations.Add(nameof(EntitiesContext.Packages)));
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations)
                    .Callback(() => operations.Add(nameof(EntitiesContext.PackageRegistrations)));

                var service = CreateService(context: context);

                service.GetPackageDependents(id);

                Assert.Equal(nameof(EntitiesContext.WithQueryHint), operations.First());
                Assert.All(
                    operations.Skip(1).Take(operations.Count - 2),
                    o => Assert.Contains(
                        o,
                        new[]
                        {
                            nameof(EntitiesContext.PackageDependencies),
                            nameof(EntitiesContext.Packages),
                            nameof(EntitiesContext.PackageRegistrations),
                        }));
                Assert.Equal(nameof(IDisposable.Dispose), operations.Last());

                disposable.Verify(x => x.Dispose(), Times.Once);
                context.Verify(x => x.WithQueryHint(It.IsAny<string>()), Times.Once);
                context.Verify(x => x.WithQueryHint("OPTIMIZE FOR UNKNOWN"), Times.Once);
            }

            [Fact]
            public void UsesRecompileIfConfigured()
            {
                string id = "Newtonsoft.Json";

                var context = new Mock<IEntitiesContext>();
                var contentObjectService = new Mock<IContentObjectService>();
                var queryHintConfiguration = new Mock<IQueryHintConfiguration>();
                contentObjectService.Setup(x => x.QueryHintConfiguration).Returns(() => queryHintConfiguration.Object);
                queryHintConfiguration.Setup(x => x.ShouldUseRecompileForPackageDependents(id)).Returns(true);

                var entityContext = new FakeEntitiesContext();

                context.Setup(f => f.PackageDependencies).Returns(entityContext.PackageDependencies);
                context.Setup(f => f.Packages).Returns(entityContext.Packages);
                context.Setup(f => f.PackageRegistrations).Returns(entityContext.PackageRegistrations);

                var service = CreateService(context: context, contentObjectService: contentObjectService);

                service.GetPackageDependents(id);

                queryHintConfiguration.Verify(x => x.ShouldUseRecompileForPackageDependents(It.IsAny<string>()), Times.Once);
                queryHintConfiguration.Verify(x => x.ShouldUseRecompileForPackageDependents(id), Times.Once);
                context.Verify(x => x.WithQueryHint(It.IsAny<string>()), Times.Once);
                context.Verify(x => x.WithQueryHint("RECOMPILE"), Times.Once);
            }

            [Fact]
            public void ThereAreExactlyFivePackagesAndAllPackagesAreVerified()
            {
                string id = "foo";
                int packageLimit = 5;

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                var packageDependenciesList = SetupPackageDependency(id);
                var packageList = SetupPackages();
                var packageRegistrationsList = SetupPackageRegistration();

                for (int i = 0; i < packageLimit; i++)
                {
                    var packageDependency = packageDependenciesList[i];
                    entityContext.PackageDependencies.Add(packageDependency);
                }

                for (int i = 0; i < packageLimit; i++)
                {
                    var package = packageList[i];
                    entityContext.Packages.Add(package);
                }

                for (int i = 0; i < packageLimit; i++)
                {
                    var packageRegistration = packageRegistrationsList[i];
                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);

                Assert.Equal(packageLimit, result.TotalPackageCount);
                Assert.Equal(packageLimit, result.TopPackages.Count);

                PackageTestsWhereAllPackagesAreVerified(result, packageLimit);
            }

            [Fact]
            public void ThereAreMoreThanFivePackagesAndAllPackagesAreVerified()
            {
                string id = "foo";

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                var packageDependenciesList = SetupPackageDependency(id);
                var packageList = SetupPackages();
                var packageRegistrationsList = SetupPackageRegistration();

                foreach (var packageDependency in packageDependenciesList)
                {
                    entityContext.PackageDependencies.Add(packageDependency);
                }

                foreach (var package in packageList)
                {
                    entityContext.Packages.Add(package);
                }

                foreach (var packageRegistration in packageRegistrationsList)
                {
                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);

                Assert.Equal(6, result.TotalPackageCount);
                Assert.Equal(5, result.TopPackages.Count);

                PackageTestsWhereAllPackagesAreVerified(result, result.TopPackages.Count);
            }

            [Fact]
            public void ThereAreLessThanFivePackagesAndAllPackagesAreVerified()
            {
                string id = "foo";
                int packageLimit = 3;

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                var packageDependenciesList = SetupPackageDependency(id);
                var packageList = SetupPackages();
                var packageRegistrationsList = SetupPackageRegistration();

                for (int i = 0; i < packageLimit; i++)
                {
                    var packageDependency = packageDependenciesList[i];
                    entityContext.PackageDependencies.Add(packageDependency);
                }

                for (int i = 0; i < packageLimit; i++)
                {
                    var package = packageList[i];
                    entityContext.Packages.Add(package);
                }

                for (int i = 0; i < packageLimit; i++)
                {
                    var packageRegistration = packageRegistrationsList[i];
                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);

                Assert.Equal(packageLimit, result.TotalPackageCount);
                Assert.Equal(packageLimit, result.TopPackages.Count);

                PackageTestsWhereAllPackagesAreVerified(result, packageLimit);
            }

            [Fact]
            public void ThereAreNoPackageDependents()
            {
                string id = "foo";

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);
                Assert.Equal(0, result.TotalPackageCount);
                Assert.Equal(0, result.TopPackages.Count);
            }

            [Fact]
            public void PackageIsNotLatestSemVer2()
            {
                string id = "foo";

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                var packageDependenciesList = SetupPackageDependency(id);
                var packageList = SetupPackages();
                var packageRegistrationsList = SetupPackageRegistration();

                foreach (var packageDependency in packageDependenciesList)
                {
                    entityContext.PackageDependencies.Add(packageDependency);
                }

                foreach (var package in packageList)
                {
                    package.IsLatestSemVer2 = false;
                    entityContext.Packages.Add(package);
                }

                foreach (var packageRegistration in packageRegistrationsList)
                {
                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);

                Assert.Equal(0, result.TotalPackageCount);
                Assert.Equal(0, result.TopPackages.Count);
            }

            [Fact]
            public void NoVerifiedPackages()
            {
                string id = "foo";

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                var packageDependenciesList = SetupPackageDependency(id);
                var packageList = SetupPackages();
                var packageRegistrationsList = SetupPackageRegistration();

                foreach (var packageDependency in packageDependenciesList)
                {
                    entityContext.PackageDependencies.Add(packageDependency);
                }

                foreach (var package in packageList)
                {
                    entityContext.Packages.Add(package);
                }

                foreach (var packageRegistration in packageRegistrationsList)
                {
                    packageRegistration.IsVerified = false;
                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);

                Assert.Equal(6, result.TotalPackageCount);
                Assert.Equal(5, result.TopPackages.Count);

                for (int i = 0; i < result.TopPackages.Count; i++)
                {
                    var currentPackage = result.TopPackages.ElementAt(i);
                    var prevPackage = i > 0 ? result.TopPackages.ElementAt(i - 1) : null;
                    if (prevPackage != null)
                    {
                        Assert.True(currentPackage.DownloadCount <= prevPackage.DownloadCount);
                    }
                    Assert.False(currentPackage.IsVerified);
                }
            }

            [Fact]
            public void MixtureOfVerifiedAndNonVerifiedPackages()
            {
                string id = "foo";
                int packageLimit = 5;

                var context = new Mock<IEntitiesContext>();
                var entityContext = new FakeEntitiesContext();

                var service = CreateService(context: context);

                var packageDependenciesList = SetupPackageDependency(id);
                var packageList = SetupPackages();
                var packageRegistrationsList = SetupPackageRegistration();

                for (int i = 0; i < packageLimit; i++)
                {
                    var packageDependency = packageDependenciesList[i];
                    entityContext.PackageDependencies.Add(packageDependency);
                }

                for (int i = 0; i < packageLimit; i++)
                {
                    var package = packageList[i];
                    entityContext.Packages.Add(package);
                }

                for (int i = 0; i < packageLimit; i++)
                {
                    var packageRegistration = packageRegistrationsList[i];

                    if (i % 2 == 0)
                    {
                        packageRegistration.IsVerified = false;
                    }

                    entityContext.PackageRegistrations.Add(packageRegistration);
                }

                context
                    .Setup(f => f.PackageDependencies)
                    .Returns(entityContext.PackageDependencies);
                context
                    .Setup(f => f.Packages)
                    .Returns(entityContext.Packages);
                context
                    .Setup(f => f.PackageRegistrations)
                    .Returns(entityContext.PackageRegistrations);

                var result = service.GetPackageDependents(id);

                Assert.Equal(packageLimit, result.TotalPackageCount);
                Assert.Equal(packageLimit, result.TopPackages.Count);

                for (int i = 0; i < packageLimit; i++)
                {
                    var currentPackage = result.TopPackages.ElementAt(i);
                    var prevPackage = i > 0 ? result.TopPackages.ElementAt(i - 1) : null;
                    if (prevPackage != null)
                    {
                        Assert.True(currentPackage.DownloadCount <= prevPackage.DownloadCount);
                    }

                    if (i % 2 == 0)
                    {
                        Assert.False(currentPackage.IsVerified);
                    }

                    else 
                    {
                        Assert.True(currentPackage.IsVerified);
                    }    
                }
            }

            private void PackageTestsWhereAllPackagesAreVerified(PackageDependents result, int packages)
            {
                for (int i = 0; i < packages; i++)
                {
                    var currentPackage = result.TopPackages.ElementAt(i);
                    var prevPackage = i > 0 ? result.TopPackages.ElementAt(i - 1) : null;
                    if (prevPackage != null)
                    {
                        Assert.True(currentPackage.DownloadCount <= prevPackage.DownloadCount);
                    }
                    Assert.True(currentPackage.IsVerified);
                }
            }

            private List<PackageDependency> SetupPackageDependency(string id)
            {
                var packageDependencyList = new List<PackageDependency>();

                var foo1 = new PackageDependency()
                {
                    PackageKey = 1,
                    Id = id
                };

                var foo2 = new PackageDependency()
                {
                    PackageKey = 2,
                    Id = id
                };

                var foo3 = new PackageDependency()
                {
                    PackageKey = 3,
                    Id = id
                };

                var foo4 = new PackageDependency()
                {
                    PackageKey = 4,
                    Id = id
                };

                var foo5 = new PackageDependency()
                {
                    PackageKey = 5,
                    Id = id
                };

                var foo6 = new PackageDependency()
                {
                    PackageKey = 6,
                    Id = id
                };

                packageDependencyList.Add(foo1);
                packageDependencyList.Add(foo2);
                packageDependencyList.Add(foo3);
                packageDependencyList.Add(foo4);
                packageDependencyList.Add(foo5);
                packageDependencyList.Add(foo6);

                return packageDependencyList;
            }

            private List<Package> SetupPackages()
            {
                var packagesList = new List<Package>();

                var pFoo1 = new Package()
                {
                    Key = 1,
                    PackageRegistrationKey = 11,
                    IsLatestSemVer2 = true,
                    Description = "This 111"
                };

                var pFoo2 = new Package()
                {
                    Key = 2,
                    PackageRegistrationKey = 22,
                    IsLatestSemVer2 = true,
                    Description = "This 222"
                };

                var pFoo3 = new Package()
                {
                    Key = 3,
                    PackageRegistrationKey = 33,
                    IsLatestSemVer2 = true,
                    Description = "This 333"
                };

                var pFoo4 = new Package()
                {
                    Key = 4,
                    PackageRegistrationKey = 44,
                    IsLatestSemVer2 = true,
                    Description = "This 444"
                };

                var pFoo5 = new Package()
                {
                    Key = 5,
                    PackageRegistrationKey = 55,
                    IsLatestSemVer2 = true,
                    Description = "This 555"
                };

                var pFoo6 = new Package()
                {
                    Key = 6,
                    PackageRegistrationKey = 66,
                    IsLatestSemVer2 = true,
                    Description = "I put the 7 on purpose 667"
                };

                packagesList.Add(pFoo1);
                packagesList.Add(pFoo2);
                packagesList.Add(pFoo3);
                packagesList.Add(pFoo4);
                packagesList.Add(pFoo5);
                packagesList.Add(pFoo6);

                return packagesList;
            }

            private List<PackageRegistration> SetupPackageRegistration()
            {
                var packageRegistrationList= new List<PackageRegistration>();

                var prFoo1 = new PackageRegistration()
                {
                    Key = 11,
                    DownloadCount = 100,
                    Id = "p1",
                    IsVerified = true
                };

                var prFoo2 = new PackageRegistration()
                {
                    Key = 22,
                    DownloadCount = 200,
                    Id = "p2",
                    IsVerified = true
                };

                var prFoo3 = new PackageRegistration()
                {
                    Key = 33,
                    DownloadCount = 300,
                    Id = "p3",
                    IsVerified = true
                };

                var prFoo4 = new PackageRegistration()
                {
                    Key = 44,
                    DownloadCount = 400,
                    Id = "p4",
                    IsVerified = true
                };

                var prFoo5 = new PackageRegistration()
                {
                    Key = 55,
                    DownloadCount = 500,
                    Id = "p5",
                    IsVerified = true
                };
                var prFoo6 = new PackageRegistration()
                {
                    Key = 66,
                    DownloadCount = 600,
                    Id = "p6",
                    IsVerified = true
                };

                packageRegistrationList.Add(prFoo1);
                packageRegistrationList.Add(prFoo2);
                packageRegistrationList.Add(prFoo3);
                packageRegistrationList.Add(prFoo4);
                packageRegistrationList.Add(prFoo5);
                packageRegistrationList.Add(prFoo6);

                return packageRegistrationList;
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("       ")]
            public void WillThrowIfIdIsNullOrEmpty(string id)
            {
                var service = CreateService();
                var ex = Assert.Throws<ArgumentNullException>(() => service.GetPackageDependents(id));
                Assert.Equal("id", ex.ParamName);
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

            [Theory]
            [InlineData("readme.md", true)]
            [InlineData(null, false)]
            public void SetsHasReadmeFlagProperly(string readmeFilename, bool expectedFlag)
            {
                var service = CreateService();
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "SomePackage"
                    },
                    HasReadMe = false,
                };

                // the EnrichPackageFromNuGetPackage method does not read readme filename from the PackageArchiveReader
                // so we won't bother setting it up here.
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(package.Id);

                var packageArchiveReader = new PackageArchiveReader(packageStream);

                var metadataDictionary = new Dictionary<string, string>
                {
                    { "version", "1.2.3" },
                };

                if (readmeFilename != null)
                {
                    metadataDictionary.Add("readme", readmeFilename);
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

                Assert.Equal(expectedFlag, package.HasReadMe);
            }
        }
    }
}