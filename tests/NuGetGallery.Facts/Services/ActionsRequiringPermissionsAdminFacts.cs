// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Services
{
    public class ActionsRequiringPermissionsAdminFacts
    {
        private int _key = 0;

        public static IEnumerable<Func<ActionRequiringPackagePermissions>> PackageActions = new Func<ActionRequiringPackagePermissions>[]
        {
            () => ActionsRequiringPermissions.DisplayPrivatePackageMetadata,
            () => ActionsRequiringPermissions.DeleteSymbolPackage,
            () => ActionsRequiringPermissions.EditPackage,
            () => ActionsRequiringPermissions.UnlistOrRelistPackage,
            () => ActionsRequiringPermissions.DeprecatePackage,
            () => ActionsRequiringPermissions.ManagePackageOwnership,
        };

        public static IEnumerable<object[]> PackageActionsWithAdmin =>
            from actionProvider in PackageActions
            from isAdmin in new[] { false, true }
            select new object[] { actionProvider, isAdmin };

        [Theory]
        [MemberData(nameof(PackageActionsWithAdmin))]
        public void SatisfiedWhenAdminIsEnabledForPackagesAnyAccount(
            Func<ActionRequiringPackagePermissions> actionProvider,
            bool isAdmin)
        {
            // separate set of requirements are used for CheckPermission call is used
            // which never includes SiteAdmin check, so we are not going to test those

            ActionsRequiringPermissions.AdminAccessEnabled = isAdmin;

            var user = new User("testuser" + _key) { Key = _key++ };
            user.Roles.Add(new Role { Name = Constants.AdminRoleName });

            var pkg = new Package();
            pkg.PackageRegistration = new PackageRegistration { Owners = Array.Empty<User>() };

            var action = actionProvider();
            var result = action.CheckPermissionsOnBehalfOfAnyAccount(user, pkg);
            Assert.Equal(isAdmin, PermissionsCheckResult.Allowed == result);
        }

        public static IEnumerable<Func<ActionRequiringAccountPermissions>> AccountActions = new Func<ActionRequiringAccountPermissions>[]
        {
            () => ActionsRequiringPermissions.ViewAccount,
            () => ActionsRequiringPermissions.ManageMembership,
        };

        public static IEnumerable<object[]> AccountActionsWithAdmin =>
            from actionProvider in AccountActions
            from isAdmin in new[] { false, true }
            select new object[] { actionProvider, isAdmin };

        [Theory]
        [MemberData(nameof(AccountActionsWithAdmin))]
        public void SatisfiedDependingOnAdminEnabledForAccounts(
            Func<ActionRequiringAccountPermissions> actionProvider,
            bool isAdmin)
        {
            ActionsRequiringPermissions.AdminAccessEnabled = isAdmin;

            var user = new User("testuser" + _key) { Key = _key++ };
            user.Roles.Add(new Role { Name = Constants.AdminRoleName });

            var target = new User("testuser" + _key) { Key = _key++ };

            var action = actionProvider();
            var result = action.CheckPermissions(user, target);
            Assert.Equal(isAdmin, PermissionsCheckResult.Allowed == result);
        }

        public static IEnumerable<Func<ActionRequiringReservedNamespacePermissions>> ReservedNamespaceAction = new Func<ActionRequiringReservedNamespacePermissions>[]
        {
            () => ActionsRequiringPermissions.RemovePackageFromReservedNamespace,
        };

        public static IEnumerable<object[]> ReservedNamespaceActionWithAdmin =>
            from actionProvider in ReservedNamespaceAction
            from isAdmin in new[] { false, true }
            select new object[] { actionProvider, isAdmin };

        [Theory]
        [MemberData(nameof(ReservedNamespaceActionWithAdmin))]
        public void SatisfiedDependingOnAdminEnabledForReservedNamespaces(
            Func<ActionRequiringReservedNamespacePermissions> actionProvider,
            bool isAdmin)
        {
            ActionsRequiringPermissions.AdminAccessEnabled = isAdmin;

            var user = new User("testuser" + _key) { Key = _key++ };
            user.Roles.Add(new Role { Name = Constants.AdminRoleName });

            var target = new User("testuser" + _key) { Key = _key++ };
            var reservedNamespace = new ReservedNamespace("Prefix", false, false);
            reservedNamespace.Owners = new[] { target };

            var action = actionProvider();
            var result = action.CheckPermissions(user, target, reservedNamespace);
            Assert.Equal(isAdmin, PermissionsCheckResult.Allowed == result);
        }

        [Theory]
        [MemberData(nameof(ReservedNamespaceActionWithAdmin))]
        public void SatisfiedDependingOnAdminEnabledForReservedNamespacesAnyAccount(
            Func<ActionRequiringReservedNamespacePermissions> actionProvider,
            bool isAdmin)
        {
            ActionsRequiringPermissions.AdminAccessEnabled = isAdmin;

            var user = new User("testuser" + _key) { Key = _key++ };
            user.Roles.Add(new Role { Name = Constants.AdminRoleName });

            var target = new User("testuser" + _key) { Key = _key++ };
            var reservedNamespace = new ReservedNamespace("Prefix", false, false);
            reservedNamespace.Owners = new[] { target };

            var action = actionProvider();
            var result = action.CheckPermissionsOnBehalfOfAnyAccount(user, reservedNamespace);
            Assert.Equal(isAdmin, PermissionsCheckResult.Allowed == result);
        }
    }
}
