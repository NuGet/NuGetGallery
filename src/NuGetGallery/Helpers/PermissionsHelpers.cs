// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public static class PermissionsHelpers
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
            if (WouldSatisfy(PermissionsRequirement.None, permissionsRequirement))
            {
                return true;
            }

            if (currentPrincipal == null)
            {
                return false;
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
            if (WouldSatisfy(PermissionsRequirement.None, permissionsRequirement))
            {
                return true;
            }

            if (currentUser == null)
            {
                return false;
            }

            return IsRequirementSatisfied(
                permissionsRequirement,
                currentUser.IsAdministrator(),
                u => currentUser.MatchesUser(u),
                entityOwners);
        }

        private static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, bool isUserAdmin, Func<User, bool> isUserMatch, IEnumerable<User> entityOwners)
        {
            if (WouldSatisfy(PermissionsRequirement.None, permissionsRequirement))
            {
                return true;
            }

            if (entityOwners == null || !entityOwners.Any())
            {
                return false;
            }

            if (WouldSatisfy(PermissionsRequirement.Owner, permissionsRequirement) &&
                entityOwners.Any(isUserMatch))
            {
                return true;
            }

            if (WouldSatisfy(PermissionsRequirement.SiteAdmin, permissionsRequirement) &&
                isUserAdmin)
            {
                return true;
            }

            var matchingMembers = entityOwners
                .OfType<Organization>()
                .SelectMany(o => o.Members)
                .Where(m => isUserMatch(m.Member))
                .ToArray();

            if (WouldSatisfy(PermissionsRequirement.OrganizationAdmin, permissionsRequirement) &&
                matchingMembers.Any(m => m.IsAdmin))
            {
                return true;
            }

            if (WouldSatisfy(PermissionsRequirement.OrganizationCollaborator, permissionsRequirement) &&
                matchingMembers.Any())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if and only if satisfying <paramref name="permissionsRequirementToCheck"/> would also satisfy <paramref name="permissionsRequirementToSatisfy"/>?
        /// </summary>
        private static bool WouldSatisfy(PermissionsRequirement permissionsRequirementToCheck, PermissionsRequirement permissionsRequirementToSatisfy)
        {
            return (permissionsRequirementToCheck & permissionsRequirementToSatisfy) > 0;
        }
    }
}