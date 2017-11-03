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
        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="package"/>?
        /// </summary>
        public static bool IsActionAllowed(Package package, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(package.PackageRegistration, currentPrincipal, actionPermissionLevel);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="packageRegistration"/>?
        /// </summary>
        public static bool IsActionAllowed(PackageRegistration packageRegistration, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(packageRegistration.Owners, currentPrincipal, actionPermissionLevel);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="account"/>?
        /// </summary>
        public static bool IsActionAllowed(User account, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(new User[] { account }, currentPrincipal, actionPermissionLevel);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on the entity owned by <paramref name="entityOwners"/>?
        /// </summary>
        public static bool IsActionAllowed(IEnumerable<User> entityOwners, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            var userPermissionLevel = GetPermissionLevel(entityOwners, currentPrincipal);
            return IsAllowed(userPermissionLevel, actionPermissionLevel);
        }

        /// <summary>
        /// Is <paramref name="currentUser"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="package"/>?
        /// </summary>
        public static bool IsActionAllowed(Package package, User currentUser, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(package.PackageRegistration, currentUser, actionPermissionLevel);
        }

        /// <summary>
        /// Is <paramref name="currentUser"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="packageRegistration"/>?
        /// </summary>
        public static bool IsActionAllowed(PackageRegistration packageRegistration, User currentUser, PermissionLevel actionPermissionLevel)
        {
            return IsActionAllowed(packageRegistration.Owners, currentUser, actionPermissionLevel);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="account"/>?
        /// </summary>
        public static bool IsActionAllowed(User account, User currentUser, PermissionLevel action)
        {
            return IsActionAllowed(new User[] { account }, currentUser, action);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on the entity owned by <paramref name="entityOwners"/>?
        /// </summary>
        public static bool IsActionAllowed(IEnumerable<User> entityOwners, User currentUser, PermissionLevel actionPermissionLevel)
        {
            var userPermissionLevel = GetPermissionLevel(entityOwners, currentUser);
            return IsAllowed(userPermissionLevel, actionPermissionLevel);
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
                currentUser.IsAdministrator(), 
                u => currentUser.MatchesUser(u));
        }

        internal static PermissionLevel GetPermissionLevel(IEnumerable<User> entityOwners, IPrincipal currentPrincipal)
        {
            if (currentPrincipal == null)
            {
                return PermissionLevel.Anonymous;
            }

            return GetPermissionLevel(
                entityOwners, 
                currentPrincipal.IsAdministrator(), 
                u => currentPrincipal.MatchesUser(u));
        }

        private static PermissionLevel GetPermissionLevel(IEnumerable<User> entityOwners, bool isUserAdmin, Func<User, bool> isUserMatch)
        {
            var permissionLevel = PermissionLevel.Anonymous;

            if (entityOwners == null)
            {
                return permissionLevel;
            }

            if (isUserAdmin)
            {
                permissionLevel |= PermissionLevel.SiteAdmin;
            }

            if (entityOwners.Any(isUserMatch))
            {
                permissionLevel |= PermissionLevel.Owner;
            }

            var matchingMembers = entityOwners
                .Where(o => o is Organization)
                .Cast<Organization>()
                .Where(o => o.Members != null)
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
    }
}