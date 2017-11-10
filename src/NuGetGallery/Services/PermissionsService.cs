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
            return HasPermission(entityOwners, currentPrincipal, actionPermissionLevel);
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
            return HasPermission(entityOwners, currentUser, actionPermissionLevel);
        }

        private static bool HasPermission(IEnumerable<User> owners, User currentUser, PermissionLevel actionPermissionLevel)
        {
            if (currentUser == null)
            {
                return PermissionLevelsMatch(PermissionLevel.Anonymous, actionPermissionLevel);
            }

            return HasPermission(
                owners, 
                currentUser.IsAdministrator(), 
                u => currentUser.MatchesUser(u),
                actionPermissionLevel);
        }

        private static bool HasPermission(IEnumerable<User> entityOwners, IPrincipal currentPrincipal, PermissionLevel actionPermissionLevel)
        {
            if (currentPrincipal == null)
            {
                return PermissionLevelsMatch(PermissionLevel.Anonymous, actionPermissionLevel);
            }

            return HasPermission(
                entityOwners, 
                currentPrincipal.IsAdministrator(), 
                u => currentPrincipal.MatchesUser(u),
                actionPermissionLevel);
        }

        private static bool HasPermission(IEnumerable<User> entityOwners, bool isUserAdmin, Func<User, bool> isUserMatch, PermissionLevel actionPermissionLevel)
        {
            if ((entityOwners == null || !entityOwners.Any()) && 
                PermissionLevelsMatch(PermissionLevel.Anonymous, actionPermissionLevel))
            {
                return true;
            }

            if (isUserAdmin && 
                PermissionLevelsMatch(PermissionLevel.SiteAdmin, actionPermissionLevel))
            {
                return true;
            }

            if (entityOwners.Any(isUserMatch) &&
                PermissionLevelsMatch(PermissionLevel.Owner, actionPermissionLevel))
            {
                return true;
            }

            var matchingMembers = entityOwners
                .Where(o => o is Organization)
                .Cast<Organization>()
                .SelectMany(o => o.Members)
                .Where(m => isUserMatch(m.Member))
                .ToArray();

            if (matchingMembers.Any(m => m.IsAdmin) &&
                PermissionLevelsMatch(PermissionLevel.OrganizationAdmin, actionPermissionLevel))
            {
                return true;
            }

            if (matchingMembers.Any() &&
                PermissionLevelsMatch(PermissionLevel.OrganizationCollaborator, actionPermissionLevel))
            {
                return true;
            }

            return PermissionLevelsMatch(PermissionLevel.Anonymous, actionPermissionLevel);
        }

        private static bool PermissionLevelsMatch(PermissionLevel first, PermissionLevel second)
        {
            return (first & second) > 0;
        }
    }
}