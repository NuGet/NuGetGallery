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
        public static PermissionLevel DisplayPrivatePackage =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.SiteAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user can upload new versions of the package from the UI.
        /// </summary>
        public static PermissionLevel UploadNewVersion =
            PermissionLevel.Owner;

        /// <summary>
        /// The user can edit existing versions of the package from the UI.
        /// </summary>
        public static PermissionLevel Edit =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.SiteAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user can unlist and relist existing versions of the package from the UI.
        /// </summary>
        public static PermissionLevel Unlist =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.SiteAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user specified by an API key's owner scope can perform all API actions on packages.
        /// </summary>
        public static PermissionLevel ApiAll = ApiPush & ApiUnlist & ApiVerify;

        /// <summary>
        /// The user specified by an API key's owner scope can push new versions an existing package using the API.
        /// </summary>
        public static PermissionLevel ApiPush = PermissionLevel.Owner;

        /// <summary>
        /// The user specified by an API key's owner scope can unlist and relist existing versions of the package using the API.
        /// </summary>
        public static PermissionLevel ApiUnlist = PermissionLevel.Owner;

        /// <summary>
        /// The user specified by an API key's owner scope can create a verification key for a package using the API.
        /// </summary>
        public static PermissionLevel ApiVerify = PermissionLevel.Owner;

        /// <summary>
        /// The user can manage ownership of the package.
        /// </summary>
        public static PermissionLevel ManagePackageOwnership =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.SiteAdmin;

        /// <summary>
        /// The user can report the package as the package's owner.
        /// This is usually used for requesting deletion of packages.
        /// </summary>
        public static PermissionLevel ReportMyPackage =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;
    }
}