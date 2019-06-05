// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using NuGet.Services.Entities;

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
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, IPrincipal currentPrincipal, ICollection<User> entityOwners)
        {
            if (currentPrincipal == null)
            {
                /// If the current principal is logged out, only <see cref="PermissionsRequirement.None"/> is satisfied.
                return WouldSatisfy(PermissionsRequirement.None, permissionsRequirement);
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
        public static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, User currentUser, ICollection<User> entityOwners)
        {
            if (currentUser == null)
            {
                /// If the current user is logged out, only <see cref="PermissionsRequirement.None"/> is satisfied.
                return WouldSatisfy(PermissionsRequirement.None, permissionsRequirement);
            }

            return IsRequirementSatisfied(
                permissionsRequirement,
                currentUser.IsAdministrator,
                u => currentUser.MatchesUser(u),
                entityOwners);
        }

        private static bool IsRequirementSatisfied(PermissionsRequirement permissionsRequirement, bool isUserAdmin, Func<User, bool> isUserMatch, ICollection<User> entityOwners)
        {
            if (isUserAdmin && 
                WouldSatisfy(PermissionsRequirement.SiteAdmin, permissionsRequirement))
            {
                return true;
            }

            if (entityOwners == null || !entityOwners.Any())
            {
                /// If there are no owners of the entity and the current user is not a site admin, only <see cref="PermissionsRequirement.None"/> is satisfied.
                return WouldSatisfy(PermissionsRequirement.None, permissionsRequirement);
            }

            if (WouldSatisfy(PermissionsRequirement.Owner, permissionsRequirement) &&
                entityOwners.Any(isUserMatch))
            {
                return true;
            }

            var entityOrganizationOwners = entityOwners
                .OfType<Organization>();

            // use cached Administrators collection to avoid querying members directly
            if (WouldSatisfy(PermissionsRequirement.OrganizationAdmin, permissionsRequirement) &&
                entityOrganizationOwners.Any(o => o.Administrators.Any(m => isUserMatch(m))))
            {
                return true;
            }

            // use cached Collaborators collection to avoid querying members directly
            if (WouldSatisfy(PermissionsRequirement.OrganizationCollaborator, permissionsRequirement) &&
                entityOrganizationOwners.Any(o => o.Collaborators.Any(m => isUserMatch(m))))
            {
                return true;
            }

            /// If the current user is not related to the entity in any way and is not a site admin, only <see cref="PermissionsRequirement.None"/> is satisfied.
            return WouldSatisfy(PermissionsRequirement.None, permissionsRequirement);
        }

        /// <summary>
        /// Returns true if and only if satisfying <paramref name="permissionsRequirementToCheck"/> would also satisfy <paramref name="permissionsRequirementToSatisfy"/>?
        /// </summary>
        private static bool WouldSatisfy(PermissionsRequirement permissionsRequirementToCheck, PermissionsRequirement permissionsRequirementToSatisfy)
        {
            return (permissionsRequirementToCheck & permissionsRequirementToSatisfy) != PermissionsRequirement.Unsatisfiable;
        }
    }
}