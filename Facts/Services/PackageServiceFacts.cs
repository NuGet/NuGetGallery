using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery {
    public class PackageServiceFacts {
        public class TheCreatePackageMethod {
            [Fact]
            public void WillCreateANewPackageRegistrationUsingTheNugetPackIdWhenOneDoesNotAlreadyExist() {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                packageRegistrationRepo.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Id == "theId")));
                packageRegistrationRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillMakeTheCurrentUserTheOwnerWhenCreatingANewPackageRegistration() {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                packageRegistrationRepo.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Owners.Contains(currentUser))));
            }

            [Fact]
            public void WillReadThePropertiesFromTheNuGetPackageWhenCreatingANewPackage() {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                // Yes, I know this is a lot of asserts. Yes, I know I broke the golden, one assert per test rule. 
                // That said, it's still asserting one "thing": that the package data was read. 
                // I'm sorry, but I just can't imagine adding a test per property.
                // Note that there is no assertion on package identifier, because that's at the package registration level (and covered in another test).
                Assert.Equal("1.0.42.0", package.Version);
                Assert.Equal("theFirstAuthor", package.Authors.ElementAt(0).Name);
                Assert.Equal("theSecondAuthor", package.Authors.ElementAt(1).Name);
                Assert.Equal("theFirstDependency", package.Dependencies.ElementAt(0).Id);
                Assert.Equal("[1.0, 2.0)", package.Dependencies.ElementAt(0).VersionRange);
                Assert.Equal("theSecondDependency", package.Dependencies.ElementAt(1).Id);
                Assert.Equal("[1.0]", package.Dependencies.ElementAt(1).VersionRange);
                Assert.Equal("theDescription", package.Description);
                Assert.Equal("http://theiconurl/", package.IconUrl);
                Assert.Equal("http://thelicenseurl/", package.LicenseUrl);
                Assert.Equal("http://theprojecturl/", package.ProjectUrl);
                Assert.Equal(true, package.RequiresLicenseAcceptance);
                Assert.Equal("theSummary", package.Summary);
                Assert.Equal("theTags", package.Tags);
                Assert.Equal("theTitle", package.Title);

                Assert.Equal("theFirstAuthor,theSecondAuthor", package.FlattenedAuthors);
                Assert.Equal("theFirstDependency:[1.0, 2.0)|theSecondDependency:[1.0]", package.FlattenedDependencies);
            }

            [Fact]
            public void WillGenerateAHashForTheCreatedPackage() {
                var service = CreateService(
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                Assert.Equal("theHash", package.Hash);
                Assert.Equal(Const.Sha512HashAlgorithmId, package.HashAlgorithm);
            }

            [Fact]
            public void WillCreateThePackageInAnUnpublishedState() {
                var service = CreateService(
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                Assert.Equal(null, package.Published);
            }

            [Fact]
            public void WillSetTheNewPackagesCreatedAndLastUpdatedTimes() {
                var service = CreateService(
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                Assert.NotEqual(DateTime.MinValue, package.Created);
                Assert.NotEqual(DateTime.MinValue, package.LastUpdated);
            }

            [Fact]
            public void WillSaveThePackageFileAndSetThePackageFileSize() {
                var packageFileSvc = new Mock<IPackageFileService>();
                var service = CreateService(
                    packageFileSvc: packageFileSvc,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                packageFileSvc.Verify(x => x.SavePackageFile(
                    "theId",
                    "1.0.42.0",
                    nugetPackage.Object.GetStream()));
                Assert.Equal(8, package.PackageFileSize);
            }

            [Fact]
            void WillSaveTheCreatedPackageWhenANewPackageRegistrationIsCreated() {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                packageRegistrationRepo.Verify(x => x.InsertOnCommit(It.Is<PackageRegistration>(pr => pr.Packages.ElementAt(0) == package)));
                packageRegistrationRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            void WillSaveTheCreatedPackageWhenThePackageRegistrationAlreadyExisted() {
                var currentUser = new User();
                var packageRegistration = new PackageRegistration {
                    Id = "theId",
                    Owners = new HashSet<User> { currentUser },
                };
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);
                    });
                var nugetPackage = CreateNuGetPackage();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                Assert.Same(packageRegistration.Packages.ElementAt(0), package);
                packageRegistrationRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            void WillThrowIfThePackageRegistrationAlreadyExistsAndTheCurrentUserIsNotAnOwner() {
                var currentUser = new User();
                var packageRegistration = new PackageRegistration {
                    Id = "theId",
                    Owners = new HashSet<User> { },
                };
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);
                    });
                var nugetPackage = CreateNuGetPackage();

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, currentUser));

                Assert.Equal(string.Format(Strings.PackageIdNotAvailable, "theId"), ex.Message);
            }
        }

        public class ThePublishPackageMethod {
            [Fact]
            public void WillSetThePublishedDateOnThePackageBeingPublished() {
                Package package = new Package {
                    Version = "1.0.42",
                    Published = null,
                    PackageRegistration = new PackageRegistration() {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");

                Assert.NotNull(package.Published);
                packageRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillSetUpdateIsLatestOnThePublishedPackageWhenItIsTheLatestVersion() {
                Package package = new Package {
                    Version = "1.0.42",
                    Published = null,
                    PackageRegistration = new PackageRegistration() {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                package.PackageRegistration.Packages.Add(new Package { Version = "1.0", PackageRegistration = package.PackageRegistration });
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");

                Assert.True(package.IsLatest);
            }

            [Fact]
            public void WillNotSetUpdateIsLatestOnThePublishedPackageWhenItIsNotTheLatestVersion() {
                Package package = new Package {
                    Version = "1.0.42",
                    Published = null,
                    PackageRegistration = new PackageRegistration() {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                package.PackageRegistration.Packages.Add(new Package { Version = "2.0", PackageRegistration = package.PackageRegistration });
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");

                Assert.False(package.IsLatest);
            }

            [Fact]
            public void WillThrowIfThePackageDoesNotExist() {
                var service = CreateService(
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)null);
                    });

                var ex = Assert.Throws<EntityException>(() => service.PublishPackage("theId", "1.0.42"));

                Assert.Equal(string.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }
        }

        static Mock<IPackage> CreateNuGetPackage(Action<Mock<IPackage>> setup = null) {
            var nugetPackage = new Mock<IPackage>();

            nugetPackage.Setup(x => x.Id).Returns("theId");
            nugetPackage.Setup(x => x.Version).Returns(new Version("1.0.42.0"));

            nugetPackage.Setup(x => x.Authors).Returns(new[] { "theFirstAuthor", "theSecondAuthor" });
            nugetPackage.Setup(x => x.Dependencies).Returns(new[] 
            { 
                new NuGet.PackageDependency("theFirstDependency", new VersionSpec(){ MinVersion = new Version(1,0), MaxVersion = new Version(2,0), IsMinInclusive = true, IsMaxInclusive = false }),
                new NuGet.PackageDependency("theSecondDependency", new VersionSpec(new Version(1,0))), 
            });
            nugetPackage.Setup(x => x.Description).Returns("theDescription");
            nugetPackage.Setup(x => x.IconUrl).Returns(new Uri("http://theiconurl/"));
            nugetPackage.Setup(x => x.LicenseUrl).Returns(new Uri("http://thelicenseurl/"));
            nugetPackage.Setup(x => x.ProjectUrl).Returns(new Uri("http://theprojecturl/"));
            nugetPackage.Setup(x => x.RequireLicenseAcceptance).Returns(true);
            nugetPackage.Setup(x => x.Summary).Returns("theSummary");
            nugetPackage.Setup(x => x.Tags).Returns("theTags");
            nugetPackage.Setup(x => x.Title).Returns("theTitle");

            nugetPackage.Setup(x => x.GetStream()).Returns(new MemoryStream(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }));

            if (setup != null)
                setup(nugetPackage);

            return nugetPackage;
        }

        static IPackageService CreateService(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepo = null,
            Mock<IEntityRepository<Package>> packageRepo = null,
            Mock<IPackageFileService> packageFileSvc = null,
            Action<Mock<PackageService>> setup = null) {
            if (cryptoSvc == null) {
                cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc.Setup(x => x.HashAlgorithmId).Returns(Const.Sha512HashAlgorithmId);
                cryptoSvc.Setup(x => x.GenerateHash(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, Const.Sha512HashAlgorithmId))
                    .Returns("theHash");
            }

            packageRegistrationRepo = packageRegistrationRepo ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepo = packageRepo ?? new Mock<IEntityRepository<Package>>();
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>();

            var packageSvc = new Mock<PackageService>(
                cryptoSvc.Object,
                packageRegistrationRepo.Object,
                packageRepo.Object,
                packageFileSvc.Object);

            packageSvc.CallBase = true;

            if (setup != null)
                setup(packageSvc);

            return packageSvc.Object;
        }
    }
}
