// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Actions that a <see cref="User"/> can perform on another <see cref="User"/>.
    /// </summary>
    public static class AccountActions
    {
        /// <summary>
        /// The user can manage ownership of the package on behalf of the account.
        /// The user can accept, reject, and cancel package ownership requests on behalf of the account.
        /// </summary>
        public static PermissionLevel ManagePackageOwnersOnBehalfOf =
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
        /// The user can perform all API actions on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiAllOnBehalfOf =
            ApiPushOnBehalfOf &
            ApiPushVersionOnBehalfOf &
            ApiUnlistOnBehalfOf &
            ApiVerifyOnBehalfOf;

        /// <summary>
        /// The user can push new package IDs on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiPushOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;

        /// <summary>
        /// The user can push new versions of an existing package using the API on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiPushVersionOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user can unlist and relist existing versions of a package using the API on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiUnlistOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user can create a verification key for a package using the API on behalf of the account.
        /// </summary>
        public static PermissionLevel ApiVerifyOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;

        /// <summary>
        /// The user can push to reserved namespaces on behalf of another owner.
        /// </summary>
        public static PermissionLevel PushToReservedNamespaceOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.SiteAdmin;
    }
}