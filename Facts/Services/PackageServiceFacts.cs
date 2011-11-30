using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NuGet;
using Xunit;

namespace NuGetGallery
{
    public class PackageServiceFacts
    {
        public class TheCreatePackageMethod
        {
            [Fact]
            public void WillCreateANewPackageRegistrationUsingTheNugetPackIdWhenOneDoesNotAlreadyExist()
            {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
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
            public void WillMakeTheCurrentUserTheOwnerWhenCreatingANewPackageRegistration()
            {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
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
            public void WillReadThePropertiesFromTheNuGetPackageWhenCreatingANewPackage()
            {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
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
                Assert.False(package.IsPrerelease);

                Assert.Equal("theFirstAuthor, theSecondAuthor", package.FlattenedAuthors);
                Assert.Equal("theFirstDependency:[1.0, 2.0)|theSecondDependency:[1.0]|theThirdDependency:", package.FlattenedDependencies);
            }

            [Fact]
            public void WillReadPrereleaseFlagFromNuGetPackage()
            {
                // Arrange
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>(MockBehavior.Strict);
                packageRegistrationRepo.Setup(r => r.InsertOnCommit(It.IsAny<PackageRegistration>())).Returns(1).Verifiable();
                packageRegistrationRepo.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage(p => p.Setup(x => x.Version).Returns(new SemanticVersion("2.14.0-a")));
                var currentUser = new User();

                // Act
                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                // Assert
                Assert.Equal("2.14.0-a", package.Version);
                Assert.Equal("theFirstAuthor", package.Authors.ElementAt(0).Name);
                Assert.Equal("theSecondAuthor", package.Authors.ElementAt(1).Name);
                Assert.Equal("theFirstDependency", package.Dependencies.ElementAt(0).Id);
                Assert.Equal("[1.0, 2.0)", package.Dependencies.ElementAt(0).VersionSpec);
                Assert.Equal("theSecondDependency", package.Dependencies.ElementAt(1).Id);
                Assert.Equal("[1.0]", package.Dependencies.ElementAt(1).VersionSpec);
                Assert.Equal("theDescription", package.Description);
                Assert.Equal("http://theiconurl/", package.IconUrl);
                Assert.Equal("http://thelicenseurl/", package.LicenseUrl);
                Assert.Equal("http://theprojecturl/", package.ProjectUrl);
                Assert.Equal(true, package.RequiresLicenseAcceptance);
                Assert.Equal("theSummary", package.Summary);
                Assert.Equal("theTags", package.Tags);
                Assert.Equal("theTitle", package.Title);
                Assert.True(package.IsPrerelease);

                Assert.Equal("theFirstAuthor, theSecondAuthor", package.FlattenedAuthors);
                Assert.Equal("theFirstDependency:[1.0, 2.0)|theSecondDependency:[1.0]|theThirdDependency:", package.FlattenedDependencies);
                packageRegistrationRepo.Verify();
            }

            [Fact]
            public void WillGenerateAHashForTheCreatedPackage()
            {
                var service = CreateService(
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                Assert.Equal("theHash", package.Hash);
                Assert.Equal(Constants.Sha512HashAlgorithmId, package.HashAlgorithm);
            }

            [Fact]
            public void WillNotCreateThePackageInAnUnpublishedState()
            {
                var service = CreateService(
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                    });
                var nugetPackage = CreateNuGetPackage();
                var currentUser = new User();

                var package = service.CreatePackage(
                    nugetPackage.Object,
                    currentUser);

                Assert.NotNull(package.Published);
            }

            [Fact]
            public void WillSetTheNewPackagesCreatedAndLastUpdatedTimes()
            {
                var service = CreateService(
                    setup: mockPackageSvc =>
                    {
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
            public void WillSaveThePackageFileAndSetThePackageFileSize()
            {
                var packageFileSvc = new Mock<IPackageFileService>();
                var service = CreateService(
                    packageFileSvc: packageFileSvc,
                    setup: mockPackageSvc =>
                    {
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
            void WillSaveTheCreatedPackageWhenANewPackageRegistrationIsCreated()
            {
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
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
            void WillSaveTheCreatedPackageWhenThePackageRegistrationAlreadyExisted()
            {
                var currentUser = new User();
                var packageRegistration = new PackageRegistration
                {
                    Id = "theId",
                    Owners = new HashSet<User> { currentUser },
                };
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
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
            void WillThrowIfThePackageRegistrationAlreadyExistsAndTheCurrentUserIsNotAnOwner()
            {
                var currentUser = new User();
                var packageRegistration = new PackageRegistration
                {
                    Id = "theId",
                    Owners = new HashSet<User> { },
                };
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(packageRegistration);
                    });
                var nugetPackage = CreateNuGetPackage();

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, currentUser));

                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageIdIsLongerThan128()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Id).Returns("theId".PadRight(129, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Id", "128"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageAuthorsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Authors).Returns(new[] { "theFirstAuthor".PadRight(2001, '_'), "theSecondAuthor".PadRight(2001, '_') });

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Authors", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageCopyrightIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Copyright).Returns("theCopyright".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Copyright", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageDependenciesIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Dependencies).Returns(new[] { 
                    new NuGet.PackageDependency("theFirstDependency".PadRight(2000, '_'), new VersionSpec { 
                        MinVersion = new SemanticVersion("1.0"), 
                        MaxVersion = new SemanticVersion("2.0"), 
                        IsMinInclusive = true, 
                        IsMaxInclusive = false }),
                    new NuGet.PackageDependency("theSecondDependency".PadRight(2000, '_'), new VersionSpec(new SemanticVersion("1.0"))), 
                });

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Dependencies", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageDescriptionIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Description).Returns("theDescription".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Description", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageIconUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.IconUrl).Returns(new Uri("http://theIconUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "IconUrl", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageLicenseUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.LicenseUrl).Returns(new Uri("http://theLicenseUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "LicenseUrl", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageProjectUrlIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.ProjectUrl).Returns(new Uri("http://theProjectUrl/".PadRight(4001, '-'), UriKind.Absolute));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "ProjectUrl", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageSummaryIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Summary).Returns("theSummary".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Summary", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageTagsIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Tags).Returns("theTags".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Tags", "4000"), ex.Message);
            }

            [Fact]
            void WillThrowIfTheNuGetPackageTitleIsLongerThan4000()
            {
                var service = CreateService();
                var nugetPackage = CreateNuGetPackage();
                nugetPackage.Setup(x => x.Title).Returns("theTitle".PadRight(4001, '_'));

                var ex = Assert.Throws<EntityException>(() => service.CreatePackage(nugetPackage.Object, null));

                Assert.Equal(String.Format(Strings.NuGetPackagePropertyTooLong, "Title", "4000"), ex.Message);
            }
        }

        public class TheDeletePackageMethod
        {
            [Fact]
            public void WillDeleteThePackage()
            {
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockSvc =>
                    {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageRepo.Verify(x => x.DeleteOnCommit(package));
                packageRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillDeleteThePackageFile()
            {
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration };
                var packageFileSvc = new Mock<IPackageFileService>();
                var service = CreateService(
                    packageFileSvc: packageFileSvc,
                    setup: mockSvc =>
                    {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageFileSvc.Verify(x => x.DeletePackageFile("theId", "1.0.42"));
            }

            [Fact]
            public void WillDeleteThePackageRegistrationIfThereAreNoOtherPackages()
            {
                var packageRegistration = new PackageRegistration { };
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0" };
                packageRegistration.Packages.Add(package);
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var packageRepo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepo.Setup(r => r.DeleteOnCommit(package)).Callback(() => { packageRegistration.Packages.Remove(package); });
                packageRepo.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    packageRepo: packageRepo,
                    setup: mockSvc =>
                    {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageRegistrationRepo.Verify(x => x.DeleteOnCommit(packageRegistration));
            }

            [Fact]
            public void WillNotDeleteThePackageRegistrationIfThereAreOtherPackages()
            {
                var packageRegistration = new PackageRegistration { Packages = new HashSet<Package>() };
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0" };
                packageRegistration.Packages.Add(package);
                packageRegistration.Packages.Add(new Package { Version = "0.9" });
                var packageRegistrationRepo = new Mock<IEntityRepository<PackageRegistration>>();
                var packageRepo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepo.Setup(r => r.DeleteOnCommit(package)).Callback(() => { packageRegistration.Packages.Remove(package); });
                packageRepo.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(
                    packageRegistrationRepo: packageRegistrationRepo,
                    packageRepo: packageRepo,
                    setup: mockSvc =>
                    {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)package);
                    });

                service.DeletePackage("theId", "1.0.42");

                packageRegistrationRepo.Verify(x => x.DeleteOnCommit(packageRegistration), Times.Never());
            }

            [Fact]
            public void WillUpdateIsLatest()
            {
                // Arrange
                var packages = new HashSet<Package>();
                var packageRegistration = new PackageRegistration { Packages = packages };
                var package_100 = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packages.Add(package_100);
                var package_10a = new Package { PackageRegistration = packageRegistration, Version = "1.0.0-a", IsPrerelease = true };
                packages.Add(package_10a);
                var package_09 = new Package { PackageRegistration = packageRegistration, Version = "0.9.0" };
                packages.Add(package_09);
                var packageRepo = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                packageRepo.Setup(r => r.DeleteOnCommit(package_100)).Callback(() => { packages.Remove(package_100); }).Verifiable();
                packageRepo.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(
                packageRepo: packageRepo,
                    setup: mockSvc =>
                    {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package_100);
                    });

                // Act
                service.DeletePackage("A", "1.0.0");

                // Assert
                Assert.True(package_10a.IsLatest);
                Assert.False(package_10a.IsLatestStable);
                Assert.False(package_09.IsLatest);
                Assert.True(package_09.IsLatestStable);
                packageRepo.Verify();
            }

            [Fact]
            public void WillThrowIfThePackageDoesNotExist()
            {
                var service = CreateService(
                    setup: mockSvc =>
                    {
                        mockSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), false)).Returns((Package)null);
                    });

                var ex = Assert.Throws<EntityException>(() => service.DeletePackage("theId", "1.0.42"));

                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }
        }

        public class TheFindPackageByIdAndVersionMethod
        {
            [Fact]
            public void WillGetTheLatestVersionWhenTheVersionArgumentIsNull()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var packages = new[] {
                    new Package { Version = "1.0", PackageRegistration = packageRegistration },
                    new Package { Version = "2.0", PackageRegistration = packageRegistration, IsLatestStable = true, IsLatest = true } 
                }.AsQueryable();
                var packageRepo = new Mock<IEntityRepository<Package>>();
                packageRepo.Setup(r => r.GetAll()).Returns(packages);
                var service = CreateService(packageRepo: packageRepo);

                var package = service.FindPackageByIdAndVersion("theId", null);

                Assert.Equal("2.0", package.Version);
            }

            [Fact]
            public void WillGetSpecifiedVersionWhenTheVersionArgumentIsNotNull()
            {
                var service = CreateService(
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Throws(new Exception("This should not be called when the version is specified."));
                    });

                Assert.DoesNotThrow(() => service.FindPackageByIdAndVersion("theId", "1.0.42"));

                // Nothing to assert because it's too damn complicated to test the actual LINQ expression.
                // What we're testing via the throw above is that it didn't load the registration and get the latest version.
            }

            [Fact]
            public void WillThrowIfIdIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() => service.FindPackageByIdAndVersion(null, "1.0.42"));

                Assert.Equal("id", ex.ParamName);
            }

            [Fact]
            public void FindPackageReturnsTheLatestVersionIfAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var package = CreatePackage("Foo", "1.0.0");
                package.IsLatest = true;
                package.IsLatestStable = true;
                var packageA = CreatePackage("Foo", "1.0.0a");

                repository.Setup(repo => repo.GetAll())
                          .Returns(new[] { package, packageA }.AsQueryable());
                var service = CreateService(packageRepo: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("Foo", version: null);

                // Assert
                Assert.Equal(package, result);
            }

            [Fact]
            public void FindPackageReturnsTheLatestVersionIfNoLatestStableVersionIsAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var package = CreatePackage("Foo", "1.0.0b");
                package.IsLatest = true;
                var packageA = CreatePackage("Foo", "1.0.0a");

                repository.Setup(repo => repo.GetAll())
                          .Returns(new[] { package, packageA }.AsQueryable());
                var service = CreateService(packageRepo: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("Foo", null);

                // Assert
                Assert.Equal(package, result);
            }

            [Fact]
            public void FindPackageReturnsTheLatestVersionIfNoLatestVersionIsAvailable()
            {
                // Arrange
                var repository = new Mock<IEntityRepository<Package>>(MockBehavior.Strict);
                var package = CreatePackage("Foo", "1.0.0b");
                var packageA = CreatePackage("Foo", "1.0.0a");

                repository.Setup(repo => repo.GetAll())
                          .Returns(new[] { package, packageA }.AsQueryable());
                var service = CreateService(packageRepo: repository);

                // Act
                var result = service.FindPackageByIdAndVersion("Foo", null);

                // Assert
                Assert.Equal(package, result);
        }
        }

        public class TheMarkPackageListedMethod
        {
            [Fact]
            public void SetsListedToTrue()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var package = new Package { Version = "1.0", PackageRegistration = packageRegistration, Listed = false };
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepo: packageRepo);

                service.MarkPackageListed(package);

                Assert.True(package.Listed);
            }

            [Fact]
            public void OnPackageVersionHigherThanLatestSetsItToLatestVersion()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var packages = new[] { 
                    new Package { Version = "1.0.1", PackageRegistration = packageRegistration, Listed = false, IsLatest = false, IsLatestStable = false },
                    new Package { Version = "1.0.0", PackageRegistration = packageRegistration, Listed = true, IsLatest = true, IsLatestStable = true }
                }.ToList();
                packageRegistration.Packages = packages;
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepo: packageRepo);

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
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepo: packageRepo);

                service.MarkPackageUnlisted(package);

                Assert.False(package.Listed);
            }

            [Fact]
            public void OnLatestPackageVersionSetsPreviousToLatestVersion()
            {
                var packageRegistration = new PackageRegistration { Id = "theId" };
                var packages = new[] { 
                    new Package { Version = "1.0.1", PackageRegistration = packageRegistration, IsLatest = true, IsLatestStable = true },
                    new Package { Version = "1.0.0", PackageRegistration = packageRegistration, IsLatest = false, IsLatestStable = false }
                }.ToList();
                packageRegistration.Packages = packages;
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepo: packageRepo);

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
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(packageRepo: packageRepo);

                service.MarkPackageUnlisted(package);

                Assert.False(package.IsLatest, "IsLatest");
                Assert.False(package.IsLatestStable, "IsLatestStable");
            }
        }

        private static Package CreatePackage(string id, string version)
        {
            return new Package
            {
                PackageRegistration = new PackageRegistration { Id = id },
                Version = version
            };
        }

        public class ThePublishPackageMethod
        {
            [Fact]
            public void WillSetThePublishedDateOnThePackageBeingPublished()
            {
                Package package = new Package
                {
                    Version = "1.0.42",
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");

                Assert.NotNull(package.Published);
                packageRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillSetUpdateIsLatestStableOnThePackageWhenItIsTheLatestVersion()
            {
                Package package = new Package
                {
                    Version = "1.0.42",
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                package.PackageRegistration.Packages.Add(new Package { Version = "1.0", PackageRegistration = package.PackageRegistration });
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");

                Assert.True(package.IsLatestStable);
            }

            [Fact]
            public void WillNotSetUpdateIsLatestStableOnThePackageWhenItIsNotTheLatestVersion()
            {
                Package package = new Package
                {
                    Version = "1.0.42",
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                package.PackageRegistration.Packages.Add(new Package
                {
                    Version = "2.0",
                    PackageRegistration = package.PackageRegistration,
                    Published = DateTime.UtcNow
                });
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42");

                Assert.False(package.IsLatestStable);
            }

            [Fact]
            public void SetUpdateUpdatesIsAbsoluteLatestForPrereleasePackage()
            {
                Package package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.Now,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    },
                    IsPrerelease = true,

                };
                package.PackageRegistration.Packages.Add(package);
                var package_39 = new Package
                {
                    Version = "1.0.39",
                    PackageRegistration = package.PackageRegistration,
                    Published = DateTime.Now.AddDays(-1)
                };
                package.PackageRegistration.Packages.Add(package_39);
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42-alpha");
                Assert.True(package_39.IsLatestStable);
                Assert.False(package_39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public void SetUpdateDoesNotSetIsLatestStableForAnyIfAllPackagesArePrerelease()
            {
                Package package = new Package
                {
                    Version = "1.0.42-alpha",
                    Published = DateTime.Now,
                    IsPrerelease = true,
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "theId",
                        Packages = new HashSet<Package>()
                    }
                };
                package.PackageRegistration.Packages.Add(package);
                var package_39 = new Package
                {
                    Version = "1.0.39-beta",
                    PackageRegistration = package.PackageRegistration,
                    Published = DateTime.Now.AddDays(-1),
                    IsPrerelease = true
                };
                package.PackageRegistration.Packages.Add(package_39);
                var packageRepo = new Mock<IEntityRepository<Package>>();
                var service = CreateService(
                    packageRepo: packageRepo,
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns(package);
                    });

                service.PublishPackage("theId", "1.0.42-alpha");
                Assert.False(package_39.IsLatestStable);
                Assert.False(package_39.IsLatest);
                Assert.False(package.IsLatestStable);
                Assert.True(package.IsLatest);
            }

            [Fact]
            public void WillThrowIfThePackageDoesNotExist()
            {
                var service = CreateService(
                    setup: mockPackageSvc =>
                    {
                        mockPackageSvc.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), true)).Returns((Package)null);
                    });

                var ex = Assert.Throws<EntityException>(() => service.PublishPackage("theId", "1.0.42"));

                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), ex.Message);
            }
        }

        public class TheAddDownloadStatisticsMethod
        {
            [Fact]
            public void WillInsertNewRecordIntoTheStatisticsRepository()
            {
                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(package, "::1", "Unit Test");

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

                service.AddDownloadStatistics(package, "::1", "Unit Test");

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.IPAddress == "unknown")));
                packageStatsRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public void WillAllowNullsForUserAgentAndUserHostAddress()
            {
                var packageStatsRepo = new Mock<IEntityRepository<PackageStatistics>>();
                var service = CreateService(packageStatsRepo: packageStatsRepo);
                var package = new Package();

                service.AddDownloadStatistics(package, null, null);

                packageStatsRepo.Verify(x => x.InsertOnCommit(It.Is<PackageStatistics>(p => p.Package == package)));
                packageStatsRepo.Verify(x => x.CommitChanges());
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

                packageOwnerRequestRepository.Verify(r => r.InsertOnCommit(
                    It.Is<PackageOwnerRequest>(req => req.PackageRegistrationKey == 1 && req.RequestingOwnerKey == 100 && req.NewOwnerKey == 200))
                );
            }

            [Fact]
            public void ReturnsExistingMatchingPackageOwnerRequest()
            {
                var packageOwnerRequestRepository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                packageOwnerRequestRepository.Setup(r => r.GetAll()).Returns(new[]{
                    new PackageOwnerRequest { 
                        PackageRegistrationKey = 1, 
                        RequestingOwnerKey = 99,
                        NewOwnerKey = 200}
                }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: packageOwnerRequestRepository);
                var package = new PackageRegistration { Key = 1 };
                var owner = new User { Key = 100 };
                var newOwner = new User { Key = 200 };

                var request = service.CreatePackageOwnerRequest(package, owner, newOwner);

                Assert.Equal(99, request.RequestingOwnerKey);
            }
        }

        public class TheRemovePackageOwnerMethod
        {
            [Fact]
            public void RemovesPackageOwner()
            {
                var service = CreateService();
                var owner = new User { };
                var package = new PackageRegistration { Owners = new List<User> { owner } };

                service.RemovePackageOwner(package, owner);

                Assert.DoesNotContain(owner, package.Owners);
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
                var owner = new User { };
                var package = new PackageRegistration { Key = 1, Owners = new List<User> { owner } };

                service.RemovePackageOwner(package, pendingOwner);

                Assert.Contains(owner, package.Owners);
                packageOwnerRequestRepository.VerifyAll();
            }
        }

        public class TheConfirmPackageOwnerMethod
        {
            [Fact]
            public void WithValidUserAndMatchingTokenReturnsTrue()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepo = new Mock<IEntityRepository<Package>>();
                packageRepo.Setup(r => r.CommitChanges()).Verifiable();
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(new[] 
                { 
                    new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token"},
                    new PackageOwnerRequest { PackageRegistrationKey = 2, NewOwnerKey = 100, ConfirmationCode = "secret-token"} 

                }.AsQueryable());
                var service = CreateService(packageRepo: packageRepo, packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.True(result);
                Assert.Contains(pendingOwner, package.Owners);
                packageRepo.VerifyAll();
            }

            [Fact]
            public void WhenUserIsAlreadyOwnerReturnsTrue()
            {
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var package = new PackageRegistration { Key = 2, Id = "pkg42", Owners = new[] { pendingOwner } };
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(new[] 
                { 
                    new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token"},

                }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.True(result);
            }

            [Fact]
            public void WithNoMatchingPackgageOwnerRequestReturnsFalse()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(new[] 
                { 
                    new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token"},

                }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.False(result);
            }

            [Fact]
            public void WithValidUserAndNonMatchingTokenReturnsFalse()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepo = new Mock<IEntityRepository<Package>>();
                packageRepo.Setup(r => r.CommitChanges()).Throws(new InvalidOperationException());
                var repository = new Mock<IEntityRepository<PackageOwnerRequest>>();
                repository.Setup(r => r.GetAll()).Returns(new[] 
                { 
                    new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token"},
                    new PackageOwnerRequest { PackageRegistrationKey = 2, NewOwnerKey = 100, ConfirmationCode = "wrong-token"} 

                }.AsQueryable());
                var service = CreateService(packageRepo: packageRepo, packageOwnerRequestRepo: repository);

                var result = service.ConfirmPackageOwner(package, pendingOwner, "secret-token");

                Assert.False(result);
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

        public class TheAddPackageOwnerMethod
        {
            [Fact]
            public void AddsUserToPackageOwnerCollection()
            {
                var package = new PackageRegistration { Key = 2, Id = "pkg42" };
                var pendingOwner = new User { Key = 100, Username = "teamawesome" };
                var packageRepo = new Mock<IEntityRepository<Package>>();
                packageRepo.Setup(r => r.CommitChanges()).Verifiable();
                var service = CreateService(packageRepo: packageRepo);

                service.AddPackageOwner(package, pendingOwner);

                Assert.Contains(pendingOwner, package.Owners);
                packageRepo.VerifyAll();
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
                repository.Setup(r => r.GetAll()).Returns(new[] 
                { 
                    new PackageOwnerRequest { PackageRegistrationKey = 1, NewOwnerKey = 100, ConfirmationCode = "super-secret-token"},
                    packageOwnerRequest

                }.AsQueryable());
                var service = CreateService(packageOwnerRequestRepo: repository);

                service.AddPackageOwner(package, pendingOwner);

                repository.VerifyAll();
            }
        }

        static Mock<IPackage> CreateNuGetPackage(Action<Mock<IPackage>> setup = null)
        {
            var nugetPackage = new Mock<IPackage>();

            nugetPackage.Setup(x => x.Id).Returns("theId");
            nugetPackage.Setup(x => x.Version).Returns(new SemanticVersion("1.0.42.0"));

            nugetPackage.Setup(x => x.Authors).Returns(new[] { "theFirstAuthor", "theSecondAuthor" });
            nugetPackage.Setup(x => x.Dependencies).Returns(new[] 
            { 
                new NuGet.PackageDependency("theFirstDependency", new VersionSpec { 
                    MinVersion = new SemanticVersion("1.0"), 
                    MaxVersion = new SemanticVersion("2.0"), 
                    IsMinInclusive = true, 
                    IsMaxInclusive = false 
                }),
                new NuGet.PackageDependency("theSecondDependency", new VersionSpec(new SemanticVersion("1.0"))),
                new NuGet.PackageDependency("theThirdDependency")
            });
            nugetPackage.Setup(x => x.Description).Returns("theDescription");
            nugetPackage.Setup(x => x.ReleaseNotes).Returns("theReleaseNotes");
            nugetPackage.Setup(x => x.IconUrl).Returns(new Uri("http://theiconurl/"));
            nugetPackage.Setup(x => x.LicenseUrl).Returns(new Uri("http://thelicenseurl/"));
            nugetPackage.Setup(x => x.ProjectUrl).Returns(new Uri("http://theprojecturl/"));
            nugetPackage.Setup(x => x.RequireLicenseAcceptance).Returns(true);
            nugetPackage.Setup(x => x.Summary).Returns("theSummary");
            nugetPackage.Setup(x => x.Tags).Returns("theTags");
            nugetPackage.Setup(x => x.Title).Returns("theTitle");
            nugetPackage.Setup(x => x.Copyright).Returns("theCopyright");

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
            Mock<IEntityRepository<PackageOwnerRequest>> packageOwnerRequestRepo = null,
            Mock<IIndexingService> indexingSvc = null,
            Action<Mock<PackageService>> setup = null)
        {
            if (cryptoSvc == null)
            {
                cryptoSvc = new Mock<ICryptographyService>();
                cryptoSvc.Setup(x => x.GenerateHash(new byte[] { 0, 0, 1, 0, 1, 0, 1, 0 }, Constants.Sha512HashAlgorithmId))
                    .Returns("theHash");
            }

            packageRegistrationRepo = packageRegistrationRepo ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepo = packageRepo ?? new Mock<IEntityRepository<Package>>();
            packageFileSvc = packageFileSvc ?? new Mock<IPackageFileService>();
            packageStatsRepo = packageStatsRepo ?? new Mock<IEntityRepository<PackageStatistics>>();
            packageOwnerRequestRepo = packageOwnerRequestRepo ?? new Mock<IEntityRepository<PackageOwnerRequest>>();
            indexingSvc = indexingSvc ?? new Mock<IIndexingService>();

            var packageSvc = new Mock<PackageService>(
                cryptoSvc.Object,
                packageRegistrationRepo.Object,
                packageRepo.Object,
                packageStatsRepo.Object,
                packageFileSvc.Object,
                packageOwnerRequestRepo.Object,
                indexingSvc.Object);

            packageSvc.CallBase = true;

            if (setup != null)
                setup(packageSvc);

            return packageSvc.Object;
        }
    }
}