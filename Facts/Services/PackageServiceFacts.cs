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
                Assert.Equal("theFirstDependency:[1.0, 2.0)|theSecondDependency:[1.0]|theThirdDependency:", package.FlattenedDependencies);
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

                packageFileSvc.Verify(x => x.SavePackageFile(package, nugetPackage.Object.GetStream()));
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

            [Fact]
            void WillThrowIfTheNuGetPackageIdIsLongerThan128() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Id).Returns("theId".PadRight(129, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Id", "128"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageAuthorsIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Authors).Returns(new[] { "theFirstAuthor".PadRight(2001, '_'), "theSecondAuthor".PadRight(2001, '_') });

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Authors", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageDependenciesIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Dependencies).Returns(new[] { 
                    new NuGet.PackageDependency("theFirstDependency".PadRight(2000, '_'), new VersionSpec(){ MinVersion = new Version(1,0), MaxVersion = new Version(2,0), IsMinInclusive = true, IsMaxInclusive = false }),
                    new NuGet.PackageDependency("theSecondDependency".PadRight(2000, '_'), new VersionSpec(new Version(1,0))), 
                });

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Dependencies", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageDescriptionIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Description).Returns("theDescription".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Description", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageIconUrlIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.IconUrl).Returns(new Uri("http://theIconUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageLicenseUrlIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.LicenseUrl).Returns(new Uri("http://theLicenseUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageProjectUrlIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.ProjectUrl).Returns(new Uri("http://theProjectUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageSummaryIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Summary).Returns("theSummary".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Summary", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageTagsIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Tags).Returns("theTags".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Tags", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageTitleIsLongerThan4000() {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Title).Returns("theTitle".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(string.Format(Strings.NuGetPackagePropertyTooLong, "Title", "4000"), ex.Message);
            }
        }

        public class TheDeletePackageMethod {
            [Fact]
            public void WillDeleteThePackage() {
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockSvc => {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageRepo.Verify(x => x.DeleteOnCommit(package));
                packageRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillDeleteThePackageFile() {
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageFileSvc = new Mock<IPackageFileService>();
                var service = CreateService(
                    packageFileSvc: packageFileSvc,
                    setup: mockSvc => {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageFileSvc.Verify(x => x.DeletePackageFile("theId", "1.0.42"));
            }

            [Fact]
            public void WillDeleteThePackageRegistrationIfThereAreNoOtherPackages() {
                var packageRegistration = new PackageRegistration { };
                var package = new Package { PackageRegistration = packageRegistration };
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockSvc => {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageRegistrationRepo.Verify(x => x.DeleteOnCommit(packageRegistration));
            }

            [Fact]
            public void WillNotDeleteThePackageRegistrationIfThereAreOtherPackages() {
                var packageRegistration = new PackageRegistration { Packages = new HashSet<Package>() };
                var package = new Package { PackageRegistration = packageRegistration };
                packageRegistration.Packages.Add(new Package());
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockSvc => {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageRegistrationRepo.Verify(x => x.DeleteOnCommit(packageRegistration), Times.Never());
            }

            [Fact]
            public void WillThrowIfThePackageDoesNotExist() {
                var service = CreateService(
                    setup: mockSvc => {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns((Package)null);
                    });

                var ex = Assert.Throws<EntityException>(() => service.DeletePackage("theId", "1.0.42"));

                Assert.Equal(string.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }
        }

        public class TheFindPackageByIdAndVersionMethod {
            [Fact]
            public void WillGetTheLatestVersionWhenTheVersionArgumentIsNull() {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var packages = new[] {
                    new Package { Version = "1.0", PackageRegistration = packageRegistration },
                    new Package { Version = "2.0", PackageRegistration = packageRegistration, IsLatest = true } 
                }.AsQueryable();
                var packageRepo = new Mock<IEntityRepository<Package>>();
                packageRepo.Setup(r => r.GetAll()).Returns(packages);
                var service = CreateService(packageRepo: packageRepo);

                var package = service.FindPackageByIdAndVersion("theId", null);

                Assert.Equal("2.0", package.Version);
            }

            [Fact]
            public void WillGetSpecifiedVersionWhenTheVersionArgumentIsNotNull() {
                var service = CreateService(
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Throws(new Exception("This should not be called when the version is specified."));
                    });

                Assert.DoesNotThrow(() => service.FindPackageByIdAndVersion("theId", "1.0.42"));

                // Nothing to assert because it's too damn complicated to test the actual LINQ expression.
                // What we're testing via the throw above is that it didn't load the registration and get the latest version.
            }

            [Fact]
            public void WillThrowIfIdIsNull() {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.FindPackageByIdAndVersion(null, "1.0.42"));

                Assert.Equal("id", ex.ParamName);
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
                package.PackageRegistration.Packages.Add(new Package {
                    Version = "2.0",
                    PackageRegistration = package.PackageRegistration,
                    Published = DateTime.UtcNow
                });
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
            public void WillNotSetUpdateIsLatestOnAnUnpublishedPackage() {
                Package package = new Package {
                    Version = "1.0.42",
                    Published = null,
                    PackageRegistration = new PackageRegistration() {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                var unpublishedPackage = new Package {
                    Version = "2.0",
                    PackageRegistration = package.PackageRegistration,
                    Published = null
                };
                package.PackageRegistration.Packages.Add(unpublishedPackage);
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc => {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>())).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");
                Assert.False(unpublishedPackage.IsLatest);
                Assert.True(package.IsLatest);
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
        
        public class TheAddDownloadStatisticsMethod {
            [Fact]
            public void WillInsertNewRecordIntoTheStatisticsRepository() {
                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(package, "::1", "Unit Test");

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.Package == package)));
                packageStatsRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillAllowNullsForUserAgentAndUserHostAddress() {
                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(package, null, null);

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.Package == package)));
                packageStatsRepo.Verify(x => x.CommitChanges());
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
                new NuGet.PackageDependency("theThirdDependency")
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
            Mock<IEntityRepository<PackageStatistics>> packageStatsRepo = null,
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
            packageStatsRepo = packageStatsRepo ?? new Mock<IEntityRepository<PackageStatistics>>();

            var packageSvc = new Mock<PackageService>(
                cryptoSvc.Object,
                packageRegistrationRepo.Object,
                packageRepo.Object,
                packageStatsRepo.Object,
                packageFileSvc.Object);

            packageSvc.CallBase = true;

            if (setup != null)
                setup(packageSvc);

            return packageSvc.Object;
        }
    }
}
