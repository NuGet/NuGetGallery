// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class PackagePermissionRestrictedActions
    {
        /// <summary>
        /// The user can accept ownership of the package.
        /// </summary>
        public static IPermissionRestrictedAction AcceptOwnership =
            new PermissionRestrictedActionExcludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
            });

        /// <summary>
        /// The user can see private information about the package.
        /// </summary>
        public static IPermissionRestrictedAction DisplayPrivatePackage =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        /// <summary>
        /// The user can upload new versions of the package.
        /// </summary>
        public static IPermissionRestrictedAction UploadNewVersion =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        /// <summary>
        /// The user can edit existing versions of the package.
        /// </summary>
        public static IPermissionRestrictedAction Edit =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        /// <summary>
        /// The user can unlist and relist existing versions of the package.
        /// </summary>
        public static IPermissionRestrictedAction Unlist =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
                PermissionLevel.OrganizationCollaborator,
            });

        /// <summary>
        /// The user can manage ownership of the package.
        /// </summary>
        public static IPermissionRestrictedAction ManagePackageOwners =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.SiteAdmin,
            });

        /// <summary>
        /// The user can report the package as the package's owner.
        /// This is usually used for requesting deletion of packages.
        /// </summary>
        public static IPermissionRestrictedAction ReportMyPackage =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
            });
    }
}