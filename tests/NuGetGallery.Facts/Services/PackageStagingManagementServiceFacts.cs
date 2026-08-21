// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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

                var result = target.GetStagedPackagesForUser(currentUser);

                Assert.Equal(
                    new[] { "Organization.Package", "Personal.Package" },
                    result.Select(stagedPackage => stagedPackage.Package.PackageRegistration.Id));
                Assert.Equal(
                    new[] { "organization", "current" },
                    result.Select(stagedPackage => stagedPackage.Owner.Username));
            }

            private static PackageStagingManagementService CreateService(
                IEnumerable<StagedPackage> stagedPackages,
                Func<User, bool> isEnabled)
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
                var entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext
                    .SetupGet(x => x.StagedPackages)
                    .Returns(stagedPackagesSet.Object);

                var featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsPackageStagingEnabled(It.IsAny<User>()))
                    .Returns((User owner) => isEnabled(owner));

                return new PackageStagingManagementService(
                    entitiesContext.Object,
                    Mock.Of<IApiScopeEvaluator>(),
                    featureFlagService.Object,
                    Mock.Of<IPackageService>());
            }

            private static StagedPackage CreateStagedPackage(int packageKey, string id, string version, User owner)
            {
                var package = new Package
                {
                    Key = packageKey,
                    NormalizedVersion = version,
                    PackageRegistration = new PackageRegistration { Id = id },
                };

                return new StagedPackage
                {
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
