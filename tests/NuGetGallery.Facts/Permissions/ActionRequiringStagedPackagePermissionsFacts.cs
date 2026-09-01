// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class ActionRequiringStagedPackagePermissionsFacts
    {
        [Fact]
        public void AllowsStagedOwnerWhoOwnsPackageRegistration()
        {
            var owner = new User("owner") { Key = 1 };
            var stagedPackage = CreateStagedPackage(owner, owner);

            var result = ActionsRequiringPermissions.ManageStagedPackage.CheckPermissions(
                owner,
                owner,
                stagedPackage);

            Assert.Equal(PermissionsCheckResult.Allowed, result);
        }

        [Fact]
        public void RejectsPackageOwnerWhoDoesNotOwnStagedAttempt()
        {
            var stagedOwner = new User("stagedOwner") { Key = 1 };
            var otherPackageOwner = new User("otherPackageOwner") { Key = 2 };
            var stagedPackage = CreateStagedPackage(stagedOwner, stagedOwner, otherPackageOwner);

            var result = ActionsRequiringPermissions.ManageStagedPackage.CheckPermissions(
                otherPackageOwner,
                otherPackageOwner,
                stagedPackage);

            Assert.Equal(PermissionsCheckResult.StagedPackageFailure, result);
        }

        [Fact]
        public void RejectsStagedOwnerWhoNoLongerOwnsPackageRegistration()
        {
            var stagedOwner = new User("stagedOwner") { Key = 1 };
            var packageOwner = new User("packageOwner") { Key = 2 };
            var stagedPackage = CreateStagedPackage(stagedOwner, packageOwner);

            var result = ActionsRequiringPermissions.ManageStagedPackage.CheckPermissions(
                stagedOwner,
                stagedOwner,
                stagedPackage);

            Assert.Equal(PermissionsCheckResult.PackageRegistrationFailure, result);
        }

        private static StagedPackage CreateStagedPackage(User stagedOwner, params User[] packageOwners)
        {
            var registration = new PackageRegistration { Id = "PackageA" };
            foreach (var packageOwner in packageOwners)
            {
                registration.Owners.Add(packageOwner);
            }

            return new StagedPackage
            {
                Owner = stagedOwner,
                OwnerKey = stagedOwner.Key,
                Package = new Package
                {
                    PackageRegistration = registration,
                },
            };
        }
    }
}
