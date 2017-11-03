// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public static class PermissionsService
    {
        public static bool IsActionAllowed(Package package, IPrincipal principal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(package.PackageRegistration, principal, actionPermissionLevel);
        }

        public static bool IsActionAllowed(PackageRegistration packageRegistration, IPrincipal principal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(packageRegistration.Owners, principal, actionPermissionLevel);
        }

        public static bool IsActionAllowed(IEnumerable<User> owners, IPrincipal principal, PermissionLevel actionPermissionLevel)
        {
            var userPermissionLevel = GetPermissionLevel(owners, principal);
            return IsAllowed(userPermissionLevel, actionPermissionLevel);
        }

        public static bool IsActionAllowed(User owner, IPrincipal principal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(new User[] { owner }, principal, actionPermissionLevel);
        }

        public static bool IsActionAllowed(Package package, User user, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(package.PackageRegistration, user, actionPermissionLevel);
        }

        public static bool IsActionAllowed(PackageRegistration packageRegistration, User user, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(packageRegistration.Owners, user, actionPermissionLevel);
        }

        public static bool IsActionAllowed(IEnumerable<User> owners, User user, PermissionLevel actionPermissionLevel)
        {
            var userPermissionLevel = GetPermissionLevel(owners, user);
            return IsAllowed(userPermissionLevel, actionPermissionLevel);
        }

        public static bool IsActionAllowed(User owner, User user, PermissionLevel action)
        {
            return IsActionAllowed(new User[] { owner }, user, action);
        }

        private static bool IsAllowed(PermissionLevel userPermissionLevel, PermissionLevel actionPermissionLevel)
        {
            return (userPermissionLevel & actionPermissionLevel) > 0;
        }

        internal static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, User user)
        {
            if (user == null)
            {
                return PermissionLevel.Anonymous;
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
                return PermissionLevel.Anonymous;
            }

            return GetPermissionLevel(
                owners, 
                principal.IsAdministrator(), 
                u => UserMatchesPrincipal(u, principal));
        }

        private static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, bool isUserAdmin, Func<User, bool> isUserMatch)
        {
            var permissionLevel = PermissionLevel.Anonymous;

            if (owners == null)
            {
                return permissionLevel;
            }

            if (isUserAdmin)
            {
                permissionLevel |= PermissionLevel.SiteAdmin;
            }

            if (owners.Any(isUserMatch))
            {
                permissionLevel |= PermissionLevel.Owner;
            }

            var matchingMembers = owners
                .Where(o => o is Organization)
                .Cast<Organization>()
                .SelectMany(o => o.Members)
                .Where(m => isUserMatch(m.Member))
                .ToArray();

            if (matchingMembers.Any(m => m.IsAdmin))
            {
                permissionLevel |= PermissionLevel.OrganizationAdmin;
            }

            if (matchingMembers.Any())
            {
                permissionLevel |= PermissionLevel.OrganizationCollaborator;
            }

            return permissionLevel;
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