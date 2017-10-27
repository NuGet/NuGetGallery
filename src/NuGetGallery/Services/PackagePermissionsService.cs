// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public enum PermissionLevel
    {
        None,
        Owner,
        SiteAdmin,
        OrganizationAdmin,
        OrganizationCollaborator
    }

    public enum Permission
    {
        DisplayMyPackage,
        UploadNewVersion,
        Edit,
        Delete,
        ManagePackageOwners,
        ReportMyPackage,
    }

    public static class PackagePermissionsService
    {
        public static bool HasPermission(Package package, IPrincipal principal, Permission permission)
        {
            return HasPermission(package.PackageRegistration, principal, permission);
        }

        public static bool HasPermission(PackageRegistration packageRegistration, IPrincipal principal, Permission permission)
        {
            return HasPermission(packageRegistration.Owners, principal, permission);
        }

        public static bool HasPermission(IEnumerable<User> owners, IPrincipal principal, Permission permission)
        {
            var permissionLevel = GetPermissionLevel(owners, principal);
            return HasPermission(permissionLevel, permission);
        }

        public static bool HasPermission(Package package, User user, Permission permission)
        {
            return HasPermission(package.PackageRegistration, user, permission);
        }

        public static bool HasPermission(PackageRegistration packageRegistration, User user, Permission permission)
        {
            return HasPermission(packageRegistration.Owners, user, permission);
        }

        public static bool HasPermission(IEnumerable<User> owners, User user, Permission permission)
        {
            var permissionLevel = GetPermissionLevel(owners, user);
            return HasPermission(permissionLevel, permission);
        }

        private static bool HasPermission(PermissionLevel permissionLevel, Permission permission)
        {
            var permissions = GetPermissions(permissionLevel);
            return permissions.Contains(permission);
        }

        internal static IEnumerable<Permission> GetPermissions(PermissionLevel permissionLevel)
        {
            switch (permissionLevel)
            {
                case PermissionLevel.Owner:
                case PermissionLevel.OrganizationAdmin:

                    yield return Permission.ReportMyPackage;

                    goto case PermissionLevel.SiteAdmin;

                case PermissionLevel.SiteAdmin:

                    yield return Permission.ManagePackageOwners;

                    goto case PermissionLevel.OrganizationCollaborator;

                case PermissionLevel.OrganizationCollaborator:

                    yield return Permission.DisplayMyPackage;
                    yield return Permission.UploadNewVersion;
                    yield return Permission.Edit;
                    yield return Permission.Delete;

                    break;

                case PermissionLevel.None:
                default:
                    yield break;
            }
        }

        internal static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, User user)
        {
            if (user == null)
            {
                return PermissionLevel.None;
            }

            return GetPermissionLevel(
                owners, 
                user.IsInRole(Constants.AdminRoleName), 
                u => UserMatchesUser(u, user));
        }

        internal static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, IPrincipal principal)
        {
            if (principal == null)
            {
                return PermissionLevel.None;
            }

            return GetPermissionLevel(
                owners, 
                principal.IsAdministrator(), 
                u => UserMatchesPrincipal(u, principal));
        }

        private static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, bool isUserAdmin, Func<User, bool> isUserMatch)
        {
            if (owners == null)
            {
                throw new ArgumentNullException(nameof(owners));
            }

            if (isUserAdmin)
            {
                return PermissionLevel.SiteAdmin;
            }

            if (owners.Any(isUserMatch))
            {
                return PermissionLevel.Owner;
            }

            var matchingMembers = owners
                .Where(u => u.Organization != null)
                .SelectMany(u => u.Organization.Memberships)
                .Where(m => isUserMatch(m.Member));

            if (matchingMembers.Any(m => m.IsAdmin))
            {
                return PermissionLevel.OrganizationAdmin;
            }

            if (matchingMembers.Any())
            {
                return PermissionLevel.OrganizationCollaborator;
            }

            return PermissionLevel.None;
        }

        private static bool UserMatchesPrincipal(User user, IPrincipal principal)
        {
            return user.Username == principal.Identity.Name;
        }

        private static bool UserMatchesUser(User first, User second)
        {
            return first.Key == second.Key;
        }
    }
}