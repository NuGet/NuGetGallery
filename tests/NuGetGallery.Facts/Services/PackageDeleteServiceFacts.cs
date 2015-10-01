// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NuGet;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class PackageDeleteServiceFacts
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

        private static IPackageDeleteService CreateService(
            Mock<IEntityRepository<PackageRegistration>> packageRegistrationRepository = null,
            Mock<IEntityRepository<Package>> packageRepository = null,
            Mock<IEntityRepository<PackageDelete>> packageDeletesRepository = null,
            Mock<IPackageService> packageService = null,
            Mock<IIndexingService> indexingService = null,
            Action<Mock<PackageDeleteService>> setup = null)
        {
            packageRegistrationRepository = packageRegistrationRepository ?? new Mock<IEntityRepository<PackageRegistration>>();
            packageRepository = packageRepository ?? new Mock<IEntityRepository<Package>>();
            packageDeletesRepository = packageDeletesRepository ?? new Mock<IEntityRepository<PackageDelete>>();
            packageService = packageService ?? new Mock<IPackageService>();
            indexingService = indexingService ?? new Mock<IIndexingService>();

            var packageDeleteService = new Mock<PackageDeleteService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                packageDeletesRepository.Object,
                packageService.Object,
                indexingService.Object);

            packageDeleteService.CallBase = true;

            if (setup != null)
            {
                setup(packageDeleteService);
            }

            return packageDeleteService.Object;
        }

        public class TheDeletePackagesAsyncMethod
        {
            [Fact]
            public async Task WillInsertNewRecordIntoThePackageDeletesRepository()
            {
                var packageDeletesRepo = new Mock<IEntityRepository<PackageDelete>>();
                var service = CreateService(packageDeletesRepository: packageDeletesRepo);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packageRegistration.Packages.Add(package);
                var user = new User("test");
                var reason = "Unit testing";
                var signature = "The Terminator";

                await service.DeletePackagesAsync(new[] { package }, user, reason, signature);

                packageDeletesRepo.Verify(x => x.InsertOnCommit(It.Is<PackageDelete>(p => p.Packages.Contains(package) && p.DeletedBy == user && p.Reason == reason && p.Signature == signature)));
                packageDeletesRepo.Verify(x => x.CommitChanges());
            }

            [Fact]
            public async Task WillUpdateThePackageRepository()
            {
                var packageRepository = new Mock<IEntityRepository<Package>>();
                var packageDeleteRepository = new Mock<IEntityRepository<PackageDelete>>();
                var service = CreateService(packageRepository: packageRepository, packageDeletesRepository: packageDeleteRepository);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.DeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                Assert.True(package.Deleted);
                packageRepository.Verify(x => x.CommitChanges());
                packageDeleteRepository.Verify(x => x.InsertOnCommit(It.IsAny<PackageDelete>()));
                packageDeleteRepository.Verify(x => x.CommitChanges());
            }

            [Fact]
            public async Task WillUpdatePackageLatestVersions()
            {
                var packageService = new Mock<IPackageService>();
                var service = CreateService(packageService: packageService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.DeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);

                packageService.Verify(x => x.UpdateIsLatest(packageRegistration, false));
            }

            [Fact]
            public async Task WillUpdateTheIndexingService()
            {
                var indexingService = new Mock<IIndexingService>();
                var service = CreateService(indexingService: indexingService);
                var packageRegistration = new PackageRegistration();
                var package = new Package { PackageRegistration = packageRegistration, Version = "1.0.0" };
                packageRegistration.Packages.Add(package);
                var user = new User("test");

                await service.DeletePackagesAsync(new[] { package }, user, string.Empty, string.Empty);
                
                indexingService.Verify(x => x.UpdateIndex(true));
            }

            [Fact]
            public void MovesPackageBinaryToBackupLocation()
            {
                // to be implemented
                Assert.False(true);
            }
        }
    }
}
