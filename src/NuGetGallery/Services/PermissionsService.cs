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
        public static bool IsActionAllowed(Package package, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(package.PackageRegistration, currentPrincipal, actionPermissionLevel);
        }

        public static bool IsActionAllowed(PackageRegistration packageRegistration, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(packageRegistration.Owners, currentPrincipal, actionPermissionLevel);
        }

        public static bool IsActionAllowed(IEnumerable<User> owners, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            var userPermissionLevel = GetPermissionLevel(owners, currentPrincipal);
            return IsAllowed(userPermissionLevel, actionPermissionLevel);
        }

        public static bool IsActionAllowed(User owner, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(new User[] { owner }, currentPrincipal, actionPermissionLevel);
        }

        public static bool IsActionAllowed(Package package, User currentUser, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(package.PackageRegistration, currentUser, actionPermissionLevel);
        }

        public static bool IsActionAllowed(PackageRegistration packageRegistration, User currentUser, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(packageRegistration.Owners, currentUser, actionPermissionLevel);
        }

        public static bool IsActionAllowed(IEnumerable<User> owners, User currentUser, PermissionLevel actionPermissionLevel)
        {
            var userPermissionLevel = GetPermissionLevel(owners, currentUser);
            return IsAllowed(userPermissionLevel, actionPermissionLevel);
        }

        public static bool IsActionAllowed(User owner, User currentUser, PermissionLevel action)
        {
            return IsActionAllowed(new User[] { owner }, currentUser, action);
        }

        private static bool IsAllowed(PermissionLevel userPermissionLevel, PermissionLevel actionPermissionLevel)
        {
            return (userPermissionLevel & actionPermissionLevel) > 0;
        }

        internal static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, User currentUser)
        {
            if (currentUser == null)
            {
                return PermissionLevel.Anonymous;
            }

            return GetPermissionLevel(
                owners, 
                currentUser.IsInRole(Constants.AdminRoleName), 
                u => UserMatchesUser(u, currentUser));
        }

        internal static PermissionLevel GetPermissionLevel(IEnumerable<User> owners, IPrincipal currentPrincipal)
        {
            if (currentPrincipal == null)
            {
                return PermissionLevel.Anonymous;
            }

            return GetPermissionLevel(
                owners, 
                currentPrincipal.IsAdministrator(), 
                u => UserMatchesPrincipal(u, currentPrincipal));
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