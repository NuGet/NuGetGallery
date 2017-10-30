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
        public static bool IsActionAllowed(Package package, IPrincipal principal, IPermissionRestrictedAction action)
        {
            return IsActionAllowed(package.PackageRegistration, principal, action);
        }

        public static bool IsActionAllowed(PackageRegistration packageRegistration, IPrincipal principal, IPermissionRestrictedAction action)
        {
            return IsActionAllowed(packageRegistration.Owners, principal, action);
        }

        public static bool IsActionAllowed(IEnumerable<User> owners, IPrincipal principal, IPermissionRestrictedAction action)
        {
            var permissionLevels = GetPermissionLevels(owners, principal);
            return action.IsAllowed(permissionLevels);
        }

        public static bool IsActionAllowed(User owner, IPrincipal principal, IPermissionRestrictedAction action)
        {
            return IsActionAllowed(new User[] { owner }, principal, action);
        }

        public static bool IsActionAllowed(Package package, User user, IPermissionRestrictedAction action)
        {
            return IsActionAllowed(package.PackageRegistration, user, action);
        }

        public static bool IsActionAllowed(PackageRegistration packageRegistration, User user, IPermissionRestrictedAction action)
        {
            return IsActionAllowed(packageRegistration.Owners, user, action);
        }

        public static bool IsActionAllowed(IEnumerable<User> owners, User user, IPermissionRestrictedAction action)
        {
            var permissionLevels = GetPermissionLevels(owners, user);
            return action.IsAllowed(permissionLevels);
        }

        public static bool IsActionAllowed(User owner, User user, IPermissionRestrictedAction action)
        {
            return IsActionAllowed(new User[] { owner }, user, action);
        }

        internal static IEnumerable<PermissionLevel> GetPermissionLevels(IEnumerable<User> owners, User user)
        {
            if (user == null)
            {
                return new[] { PermissionLevel.Anonymous };
            }

            return GetPermissionLevels(
                owners, 
                user.IsInRole(Constants.AdminRoleName), 
                u => UserMatchesUser(u, user));
        }

        internal static IEnumerable<PermissionLevel> GetPermissionLevels(IEnumerable<User> owners, IPrincipal principal)
        {
            if (principal == null)
            {
                return new[] { PermissionLevel.Anonymous };
            }

            return GetPermissionLevels(
                owners, 
                principal.IsAdministrator(), 
                u => UserMatchesPrincipal(u, principal));
        }

        private static IEnumerable<PermissionLevel> GetPermissionLevels(IEnumerable<User> owners, bool isUserAdmin, Func<User, bool> isUserMatch)
        {
            if (owners == null)
            {
                yield return PermissionLevel.Anonymous;
                yield break;
            }

            if (isUserAdmin)
            {
                yield return PermissionLevel.SiteAdmin;
            }

            if (owners.Any(isUserMatch))
            {
                yield return PermissionLevel.Owner;
            }

            var matchingMembers = owners
                .Where(u => u.Organization != null)
                .SelectMany(u => u.Organization.Memberships)
                .Where(m => isUserMatch(m.Member))
                .ToArray();

            if (matchingMembers.Any(m => m.IsAdmin))
            {
                yield return PermissionLevel.OrganizationAdmin;
            }

            if (matchingMembers.Any())
            {
                yield return PermissionLevel.OrganizationCollaborator;
            }

            yield return PermissionLevel.Anonymous;
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