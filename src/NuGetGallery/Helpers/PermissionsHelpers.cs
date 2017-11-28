// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery.Helpers
{
    public class PermissionsHelpers
    {
        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="package"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, IPrincipal currentPrincipal, Package package)
        {
            return IsActionAllowed(actionPermissionLevel, currentPrincipal, package.PackageRegistration);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="packageRegistration"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, IPrincipal currentPrincipal, PackageRegistration packageRegistration)
        {
            return IsActionAllowed(actionPermissionLevel, currentPrincipal, packageRegistration.Owners);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="account"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, IPrincipal currentPrincipal, User account)
        {
            return IsActionAllowed(actionPermissionLevel, currentPrincipal, new User[] { account });
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on the entity owned by <paramref name="entityOwners"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, IPrincipal currentPrincipal, IEnumerable<User> entityOwners)
        {
            return HasPermission(actionPermissionLevel, currentPrincipal, entityOwners);
        }

        /// <summary>
        /// Is <paramref name="currentUser"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="package"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, User currentUser, Package package)
        {
            return IsActionAllowed(actionPermissionLevel, currentUser, package.PackageRegistration);
        }

        /// <summary>
        /// Is <paramref name="currentUser"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="packageRegistration"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, User currentUser, PackageRegistration packageRegistration)
        {
            return IsActionAllowed(actionPermissionLevel, currentUser, packageRegistration.Owners);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on <paramref name="account"/>?
        /// </summary>
        public static bool IsActionAllowed(User currentUser, User account, PermissionRole action)
        {
            return IsActionAllowed(action, currentUser, new User[] { account });
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform <paramref name="actionPermissionLevel"/> on the entity owned by <paramref name="entityOwners"/>?
        /// </summary>
        public static bool IsActionAllowed(PermissionRole actionPermissionLevel, User currentUser, IEnumerable<User> entityOwners)
        {
            return HasPermission(actionPermissionLevel, currentUser, entityOwners);
        }

        private static bool HasPermission(PermissionRole actionPermissionLevel, User currentUser, IEnumerable<User> owners)
        {
            if (currentUser == null)
            {
                return PermissionLevelsIntersect(PermissionRole.Anonymous, actionPermissionLevel);
            }

            return HasPermission(
                actionPermissionLevel,
                currentUser.IsAdministrator(),
                u => currentUser.MatchesUser(u),
                owners);
        }

        private static bool HasPermission(PermissionRole actionPermissionLevel, IPrincipal currentPrincipal, IEnumerable<User> entityOwners)
        {
            if (currentPrincipal == null)
            {
                return PermissionLevelsIntersect(PermissionRole.Anonymous, actionPermissionLevel);
            }

            return HasPermission(
                actionPermissionLevel,
                currentPrincipal.IsAdministrator(),
                u => currentPrincipal.MatchesUser(u),
                entityOwners);
        }

        private static bool HasPermission(PermissionRole actionPermissionLevel, bool isUserAdmin, Func<User, bool> isUserMatch, IEnumerable<User> entityOwners)
        {
            if ((entityOwners == null || !entityOwners.Any()) &&
                PermissionLevelsIntersect(PermissionRole.Anonymous, actionPermissionLevel))
            {
                return true;
            }

            if (entityOwners.Any(isUserMatch) &&
                PermissionLevelsIntersect(PermissionRole.Owner, actionPermissionLevel))
            {
                return true;
            }

            if (isUserAdmin &&
                PermissionLevelsIntersect(PermissionRole.SiteAdmin, actionPermissionLevel))
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
                PermissionLevelsIntersect(PermissionRole.OrganizationAdmin, actionPermissionLevel))
            {
                return true;
            }

            if (matchingMembers.Any() &&
                PermissionLevelsIntersect(PermissionRole.OrganizationCollaborator, actionPermissionLevel))
            {
                return true;
            }

            return PermissionLevelsIntersect(PermissionRole.Anonymous, actionPermissionLevel);
        }

        private static bool PermissionLevelsIntersect(PermissionRole first, PermissionRole second)
        {
            return (first & second) > 0;
        }
    }
}