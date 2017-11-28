// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Actions that a <see cref="User"/> can perform on a <see cref="Package"/> or <see cref="PackageRegistration"/>.
    /// </summary>
    public static class PackageActions
    {
        /// <summary>
        /// The user can see private information about the package.
        /// </summary>
        public static PermissionRole DisplayPrivatePackage = 
            PermissionRole.Owner | 
            PermissionRole.OrganizationAdmin | 
            PermissionRole.SiteAdmin | 
            PermissionRole.OrganizationCollaborator;

        /// <summary>
        /// The user can upload new versions of the package.
        /// </summary>
        public static PermissionRole UploadNewVersion = 
            PermissionRole.Owner | 
            PermissionRole.OrganizationAdmin | 
            PermissionRole.SiteAdmin | 
            PermissionRole.OrganizationCollaborator;

        /// <summary>
        /// The user can edit existing versions of the package.
        /// </summary>
        public static PermissionRole Edit = 
            PermissionRole.Owner |
            PermissionRole.OrganizationAdmin |
            PermissionRole.SiteAdmin |
            PermissionRole.OrganizationCollaborator;

        /// <summary>
        /// The user can unlist and relist existing versions of the package.
        /// </summary>
        public static PermissionRole Unlist =
            PermissionRole.Owner |
            PermissionRole.OrganizationAdmin |
            PermissionRole.SiteAdmin |
            PermissionRole.OrganizationCollaborator;

        /// <summary>
        /// The user can manage ownership of the package.
        /// </summary>
        public static PermissionRole ManagePackageOwners =
            PermissionRole.Owner |
            PermissionRole.OrganizationAdmin |
            PermissionRole.SiteAdmin;

        /// <summary>
        /// The user can report the package as the package's owner.
        /// This is usually used for requesting deletion of packages.
        /// </summary>
        public static PermissionRole ReportMyPackage =
            PermissionRole.Owner |
            PermissionRole.OrganizationAdmin;
    }
}