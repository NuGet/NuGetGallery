﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Actions that a <see cref="User"/> can perform on another <see cref="User"/>.
    /// </summary>
    public static class AccountActions
    {
        /// <summary>
        /// If an account is requested to be an owner of a package, the user can accept the request on behalf of the account.
        /// </summary>
        public static PermissionLevel ManagePackageOwnershipOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;

        /// <summary>
        /// The user can upload new package IDs on behalf of the account using the UI.
        /// </summary>
        public static PermissionLevel UploadNewIdOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;

        /// <summary>
        /// The user can upload new versions of an existing package on behalf of the account using the UI.
        /// </summary>
        public static PermissionLevel UploadNewVersionOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user can push to reserved namespaces on behalf of another owner.
        /// </summary>
        public static PermissionLevel PushToReservedNamespaceOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;

        /// <summary>
        /// The user can see private information about an organization account.
        /// </summary>
        public static PermissionLevel DisplayPrivateOrganization =
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;
    }
}