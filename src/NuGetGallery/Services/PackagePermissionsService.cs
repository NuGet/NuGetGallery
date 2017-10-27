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
        /// <summary>
        /// The default rights to a package, held by all users on every package.
        /// </summary>
        Anonymous,

        /// <summary>
        /// The user is a direct owner of the package.
        /// </summary>
        Owner,

        /// <summary>
        /// The user is a site admin and has administrative permissions on all packages.
        /// </summary>
        SiteAdmin,

        /// <summary>
        /// The user is an administrator of an organization that is a direct owner of the package.
        /// </summary>
        OrganizationAdmin,

        /// <summary>
        /// The user is a collaborator of an organization that is a direct owner of the package.
        /// </summary>
        OrganizationCollaborator,
    }

    public enum PackageAction
    {
        /// <summary>
        /// The ability to view hidden package versions or metadata.
        /// </summary>
        DisplayPrivatePackage,

        /// <summary>
        /// The ability to upload new versions of an existing package ID.
        /// </summary>
        UploadNewVersion,

        /// <summary>
        /// The ability to edit an existing package version.
        /// </summary>
        Edit,

        /// <summary>
        /// The ability to unlist or relist an existing package version.
        /// </summary>
        Unlist,

        /// <summary>
        /// The ability to add or remove owners of the package.
        /// </summary>
        ManagePackageOwners,

        /// <summary>
        /// The ability to report a package as its owner.
        /// </summary>
        ReportMyPackage,
    }

    public static class PackagePermissionsService
    {
        private static readonly IDictionary<PermissionLevel, IEnumerable<PackageAction>> _allowedActions = 
            new Dictionary<PermissionLevel, IEnumerable<PackageAction>>
            {
                {
                    PermissionLevel.Anonymous,
                    new PackageAction[0]
                },
                {
                    PermissionLevel.OrganizationCollaborator,
                    new []
                    {
                        PackageAction.DisplayPrivatePackage,
                        PackageAction.UploadNewVersion,
                        PackageAction.Edit,
                        PackageAction.Unlist,
                    }
                },
                {
                    PermissionLevel.SiteAdmin,
                    new []
                    {
                        PackageAction.DisplayPrivatePackage,
                        PackageAction.UploadNewVersion,
                        PackageAction.Edit,
                        PackageAction.Unlist,
                        PackageAction.ManagePackageOwners,
                    }
                },
                {
                    PermissionLevel.OrganizationAdmin,
                    new []
                    {
                        PackageAction.DisplayPrivatePackage,
                        PackageAction.UploadNewVersion,
                        PackageAction.Edit,
                        PackageAction.Unlist,
                        PackageAction.ManagePackageOwners,
                        PackageAction.ReportMyPackage,
                    }
                },
                {
                    PermissionLevel.Owner,
                    new []
                    {
                        PackageAction.DisplayPrivatePackage,
                        PackageAction.UploadNewVersion,
                        PackageAction.Edit,
                        PackageAction.Unlist,
                        PackageAction.ManagePackageOwners,
                        PackageAction.ReportMyPackage,
                    }
                }
            };

        public static bool HasPermission(Package package, IPrincipal principal, PackageAction action)
        {
            return HasPermission(package.PackageRegistration, principal, action);
        }

        public static bool HasPermission(PackageRegistration packageRegistration, IPrincipal principal, PackageAction action)
        {
            return HasPermission(packageRegistration.Owners, principal, action);
        }

        public static bool HasPermission(IEnumerable<User> owners, IPrincipal principal, PackageAction action)
        {
            var permissionLevels = GetPermissionLevels(owners, principal);
            return HasPermission(permissionLevels, action);
        }

        public static bool HasPermission(Package package, User user, PackageAction action)
        {
            return HasPermission(package.PackageRegistration, user, action);
        }

        public static bool HasPermission(PackageRegistration packageRegistration, User user, PackageAction action)
        {
            return HasPermission(packageRegistration.Owners, user, action);
        }

        public static bool HasPermission(IEnumerable<User> owners, User user, PackageAction action)
        {
            var permissionLevels = GetPermissionLevels(owners, user);
            return HasPermission(permissionLevels, action);
        }

        private static bool HasPermission(IEnumerable<PermissionLevel> permissionLevels, PackageAction action)
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