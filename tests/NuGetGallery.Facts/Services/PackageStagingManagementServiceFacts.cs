// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class PackageStagingManagementServiceFacts
    {
        public class TheOwnerUiMethods
        {
            [Fact]
            public void ListsStagedPackagesForEnabledPersonalAndOrganizationOwners()
            {
                var currentUser = new User("current") { Key = 1 };
                var organization = new Organization("organization") { Key = 2 };
                var disabledOrganization = new Organization("disabled") { Key = 3 };
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = organization });
                currentUser.Organizations.Add(new Membership { Member = currentUser, Organization = disabledOrganization });

                var personalPackage = CreateStagedPackage(10, "Personal.Package", "1.0.0", currentUser);
                var organizationPackage = CreateStagedPackage(11, "Organization.Package", "2.0.0", organization);
                var disabledPackage = CreateStagedPackage(12, "Disabled.Package", "3.0.0", disabledOrganization);

                var target = CreateService(
                    new[] { personalPackage, organizationPackage, disabledPackage },
                    owner => owner != disabledOrganization);

                var result = target.GetStagedPackages(currentUser);

                Assert.Equal(
                    new[] { "Organization.Package", "Personal.Package" },
                    result.Select(stagedPackage => stagedPackage.Package.PackageRegistration.Id));
                Assert.Equal(
                    new[] { "organization", "current" },
                    result.Select(stagedPackage => stagedPackage.Owner.Username));
            }

            [Fact]
            public void ListsOnlyTheNewestAttemptForEachStagedPackage()
            {
                var currentUser = new User("current") { Key = 1 };
                var previousAttempt = CreateStagedPackage(100, 10, "Test.Package", "1.0.0", currentUser);
                previousAttempt.Status = StagedPackageStatus.Superseded;
                var currentAttempt = CreateStagedPackage(101, 10, "Test.Package", "1.0.0", currentUser);

                var target = CreateService(new[] { previousAttempt, currentAttempt }, owner => true);

                var result = target.GetStagedPackages(currentUser);

                Assert.Same(currentAttempt, Assert.Single(result));
            }

            [Fact]
            public void GetsTheNewestAttemptForAPackage()
            {
                var currentUser = new User("current") { Key = 1, EmailAddress = "current@example.test" };
                var previousOwner = new User("previous") { Key = 2 };
                var previousAttempt = CreateStagedPackage(100, 10, "Test.Package", "1.0.0", previousOwner);
                var currentAttempt = CreateStagedPackage(101, 10, "Test.Package", "1.0.0", currentUser);
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(currentAttempt.Package);
                var authorizationService = new Mock<IPackageStagingAuthorizationService>();
                authorizationService
                    .Setup(x => x.CanManageWithApiKey(
                        It.IsAny<User>(),
                        It.IsAny<IEnumerable<Scope>>(),
                        currentAttempt))
                    .Returns(true);
                var target = CreateService(
                    new[] { previousAttempt, currentAttempt },
                    owner => true,
                    authorizationService.Object,
                    packageService.Object);

                var result = target.GetPackage(currentUser, Array.Empty<Scope>(), "Test.Package", "1.0.0");

                Assert.NotNull(result);
            }

            [Theory]
            [InlineData(StagedPackageStatus.Validating, "uploaded", "uploaded-etag")]
            [InlineData(StagedPackageStatus.FailedValidation, "uploaded", "uploaded-etag")]
            [InlineData(StagedPackageStatus.Ready, "validated", "validated-etag")]
            public async Task OpensExpectedContentForCurrentAttempt(StagedPackageStatus status, string expectedPath, string expectedETag)
            {
                var currentUser = new User("current") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", currentUser);
                stagedPackage.Status = status;
                stagedPackage.UploadedBlobPath = "uploaded";
                stagedPackage.UploadedBlobETag = "uploaded-etag";
                stagedPackage.ValidatedBlobPath = "validated";
                stagedPackage.ValidatedBlobETag = "validated-etag";
                var expected = new MemoryStream();
                var stagingBlobService = new Mock<IStagingBlobService>();
                stagingBlobService
                    .Setup(x => x.OpenPackageFileAsync(expectedPath, expectedETag))
                    .ReturnsAsync(expected);
                var target = CreateService(
                    new[] { stagedPackage },
                    owner => true,
                    stagingBlobService: stagingBlobService.Object);

                var actual = await target.OpenPackageContentAsync(stagedPackage);

                Assert.Same(expected, actual);
            }

            [Fact]
            public void DoesNotFindADeletedAttempt()
            {
                var owner = new User("owner") { Key = 1 };
                var stagedPackage = CreateStagedPackage(10, "Test.Package", "1.0.0", owner);
                stagedPackage.Status = StagedPackageStatus.Deleted;
                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict("Test.Package", "1.0.0"))
                    .Returns(stagedPackage.Package);
                var target = CreateService(
                    new[] { stagedPackage },
                    user => true,
                    packageService: packageService.Object);

                var result = target.FindCurrentStagedPackage("Test.Package", "1.0.0");

                Assert.Null(result);
            }

            private static PackageStagingManagementService CreateService(
                IEnumerable<StagedPackage> stagedPackages,
                Func<User, bool> isEnabled,
                IPackageStagingAuthorizationService authorizationService = null,
                IPackageService packageService = null,
                IStagingBlobService stagingBlobService = null)
            {
                var stagedPackagesList = stagedPackages.ToList();
                var stagedPackagesQuery = stagedPackagesList.AsQueryable();
                var stagedPackagesSet = new Mock<DbSet<StagedPackage>>();
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.Provider).Returns(stagedPackagesQuery.Provider);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.Expression).Returns(stagedPackagesQuery.Expression);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.ElementType).Returns(stagedPackagesQuery.ElementType);
                stagedPackagesSet.As<IQueryable<StagedPackage>>().Setup(x => x.GetEnumerator()).Returns(() => stagedPackagesQuery.GetEnumerator());
                stagedPackagesSet.Setup(x => x.Include("Package.PackageRegistration")).Returns(stagedPackagesSet.Object);
                stagedPackagesSet.Setup(x => x.Include("Owner")).Returns(stagedPackagesSet.Object);
                var stagedPackageRepository = new Mock<IEntityRepository<StagedPackage>>();
                stagedPackageRepository
                    .Setup(x => x.GetAll())
                    .Returns(stagedPackagesSet.Object);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(It.IsAny<User>()))
                    .Returns((User owner) => isEnabled(owner));
                var defaultAuthorizationService = new Mock<IPackageStagingAuthorizationService>();
                defaultAuthorizationService
                    .Setup(x => x.CanManage(It.IsAny<User>(), It.IsAny<StagedPackage>()))
                    .Returns(true);

                return new PackageStagingManagementService(
                    authorizationService ?? defaultAuthorizationService.Object,
                    featureFlagService.Object,
                    packageService ?? Mock.Of<IPackageService>(),
                    stagedPackageRepository.Object,
                    stagingBlobService ?? Mock.Of<IStagingBlobService>());
            }

            private static StagedPackage CreateStagedPackage(int packageKey, string id, string version, User owner)
            {
                return CreateStagedPackage(packageKey, packageKey, id, version, owner);
            }

            private static StagedPackage CreateStagedPackage(int key, int packageKey, string id, string version, User owner)
            {
                var registration = new PackageRegistration { Id = id };
                registration.Owners.Add(owner);
                var package = new Package
                {
                    Key = packageKey,
                    NormalizedVersion = version,
                    PackageRegistration = registration,
                    PackageStatusKey = PackageStatus.Staged,
                };

                return new StagedPackage
                {
                    Key = key,
                    PackageKey = packageKey,
                    Package = package,
                    OwnerKey = owner.Key,
                    Owner = owner,
                    UploadedDate = DateTime.UtcNow,
                };
            }
        }
    }
}
