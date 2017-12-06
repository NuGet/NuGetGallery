// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public class PermissionsHelpers
    {
        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on <paramref name="packageRegistration"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, IPrincipal currentPrincipal, PackageRegistration packageRegistration)
        {
            return IsRequirementSatisfied(permissionsRequirement, currentPrincipal, packageRegistration.Owners);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on <paramref name="reservedNamespace"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, IPrincipal currentPrincipal, ReservedNamespace reservedNamespace)
        {
            return reservedNamespace.IsSharedNamespace || IsRequirementSatisfied(permissionsRequirement, currentPrincipal, reservedNamespace.Owners);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on <paramref name="account"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, IPrincipal currentPrincipal, User account)
        {
            return IsRequirementSatisfied(permissionsRequirement, currentPrincipal, new User[] { account });
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on the entity owned by <paramref name="entityOwners"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, IPrincipal currentPrincipal, IEnumerable<User> entityOwners)
        {
            if (currentPrincipal == null)
            {
                return PermissionLevelsIntersect(PermissionsRequirement.None, permissionsRequirement);
            }

            return IsRequirementSatisfied(
                permissionsRequirement,
                currentPrincipal.IsAdministrator(),
                u => currentPrincipal.MatchesUser(u),
                entityOwners);
        }

        /// <summary>
        /// Is <paramref name="currentUser"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on <paramref name="packageRegistration"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, User currentUser, PackageRegistration packageRegistration)
        {
            return IsRequirementSatisfied(permissionsRequirement, currentUser, packageRegistration.Owners);
        }

        /// <summary>
        /// Is <paramref name="currentUser"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on <paramref name="reservedNamespace"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, User currentUser, ReservedNamespace reservedNamespace)
        {
            return reservedNamespace.IsSharedNamespace || IsRequirementSatisfied(permissionsRequirement, currentUser, reservedNamespace.Owners);
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on <paramref name="account"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement action, User currentUser, User account)
        {
            return IsRequirementSatisfied(action, currentUser, new User[] { account });
        }

        /// <summary>
        /// Is <paramref name="currentPrincipal"/> allowed to perform an action with a requirement of <paramref name="permissionsRequirement"/> on the entity owned by <paramref name="entityOwners"/>?
        /// </summary>
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, User currentUser, IEnumerable<User> entityOwners)
        {
            if (currentUser == null)
            {
                return PermissionLevelsIntersect(PermissionsRequirement.None, permissionsRequirement);
            }

            return IsRequirementSatisfied(
                permissionsRequirement,
                currentUser.IsAdministrator(),
                u => currentUser.MatchesUser(u),
                entityOwners);
        }

        private static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, bool isUserAdmin, Func<User, bool> isUserMatch, IEnumerable<User> entityOwners)
        {
            if ((entityOwners == null || !entityOwners.Any()) &&
                PermissionLevelsIntersect(PermissionsRequirement.None, permissionsRequirement))
            {
                return true;
            }

            if (entityOwners.Any(isUserMatch) &&
                PermissionLevelsIntersect(PermissionsRequirement.Owner, permissionsRequirement))
            {
                return true;
            }

            if (isUserAdmin &&
                PermissionLevelsIntersect(PermissionsRequirement.SiteAdmin, permissionsRequirement))
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
                PermissionLevelsIntersect(PermissionsRequirement.OrganizationAdmin, permissionsRequirement))
            {
                return true;
            }

            if (matchingMembers.Any() &&
                PermissionLevelsIntersect(PermissionsRequirement.OrganizationCollaborator, permissionsRequirement))
            {
                return true;
            }

            return PermissionLevelsIntersect(PermissionsRequirement.None, permissionsRequirement);
        }

        private static bool PermissionLevelsIntersect(PermissionsRequirement first, PermissionsRequirement second)
        {
            return (first & second) > 0;
        }
    }
}