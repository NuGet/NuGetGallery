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
        Anonymous,
        Owner,
        SiteAdmin,
        OrganizationAdmin,
        OrganizationCollaborator
    }

    public enum Action
    {
        DisplayPrivatePackage,
        UploadNewVersion,
        Edit,
        Delete,
        ManagePackageOwners,
        ReportMyPackage,
    }

    public static class PackagePermissionsService
    {
        private static readonly IDictionary<PermissionLevel, IEnumerable<Action>> _allowedActions = 
            new Dictionary<PermissionLevel, IEnumerable<Action>>
            {
                {
                    PermissionLevel.Anonymous,
                    new Action[0]
                },
                {
                    PermissionLevel.OrganizationCollaborator,
                    new []
                    {
                        Action.DisplayPrivatePackage,
                        Action.UploadNewVersion,
                        Action.Edit,
                        Action.Delete,
                    }
                },
                {
                    PermissionLevel.SiteAdmin,
                    new []
                    {
                        Action.DisplayPrivatePackage,
                        Action.UploadNewVersion,
                        Action.Edit,
                        Action.Delete,
                        Action.ManagePackageOwners,
                    }
                },
                {
                    PermissionLevel.OrganizationAdmin,
                    new []
                    {
                        Action.DisplayPrivatePackage,
                        Action.UploadNewVersion,
                        Action.Edit,
                        Action.Delete,
                        Action.ManagePackageOwners,
                        Action.ReportMyPackage,
                    }
                },
                {
                    PermissionLevel.Owner,
                    new []
                    {
                        Action.DisplayPrivatePackage,
                        Action.UploadNewVersion,
                        Action.Edit,
                        Action.Delete,
                        Action.ManagePackageOwners,
                        Action.ReportMyPackage,
                    }
                }
            };

        public static bool HasPermission(Package package, IPrincipal principal, Action action)
        {
            return HasPermission(package.PackageRegistration, principal, action);
        }

        public static bool HasPermission(PackageRegistration packageRegistration, IPrincipal principal, Action action)
        {
            return HasPermission(packageRegistration.Owners, principal, action);
        }

        public static bool HasPermission(IEnumerable<User> owners, IPrincipal principal, Action action)
        {
            var permissionLevels = GetPermissionLevels(owners, principal);
            return HasPermission(permissionLevels, action);
        }

        public static bool HasPermission(Package package, User user, Action action)
        {
            return HasPermission(package.PackageRegistration, user, action);
        }

        public static bool HasPermission(PackageRegistration packageRegistration, User user, Action action)
        {
            return HasPermission(packageRegistration.Owners, user, action);
        }

        public static bool HasPermission(IEnumerable<User> owners, User user, Action action)
        {
            var permissionLevels = GetPermissionLevels(owners, user);
            return HasPermission(permissionLevels, action);
        }

        private static bool HasPermission(IEnumerable<PermissionLevel> permissionLevels, Action action)
        {
            return permissionLevels.Any(permissionLevel => _allowedActions[permissionLevel].Contains(action));
        }

        public static IEnumerable<PermissionLevel> GetPermissionLevels(Package package, User user)
        {
            return GetPermissionLevels(package, user);
        }

        public static IEnumerable<PermissionLevel> GetPermissionLevels(PackageRegistration packageRegistration, User user)
        {
            return GetPermissionLevels(packageRegistration.Owners, user);
        }

        public static IEnumerable<PermissionLevel> GetPermissionLevels(IEnumerable<User> owners, User user)
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

        public static IEnumerable<PermissionLevel> GetPermissionLevels(Package package, IPrincipal principal)
        {
            return GetPermissionLevels(package, principal);
        }

        public static IEnumerable<PermissionLevel> GetPermissionLevels(PackageRegistration packageRegistration, IPrincipal principal)
        {
            return GetPermissionLevels(packageRegistration.Owners, principal);
        }

        public static IEnumerable<PermissionLevel> GetPermissionLevels(IEnumerable<User> owners, IPrincipal principal)
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
                .Where(m => isUserMatch(m.Member));

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