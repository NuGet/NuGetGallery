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
        /// If a user is requested to be an owner of a package, this user can accept the request on behalf of the other user.
        /// </summary>
        public static PermissionLevel AcceptPackageOwnershipOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin;

        public static PermissionLevel UploadPackageOnBehalfOf =
            PermissionLevel.Owner |
            PermissionLevel.OrganizationAdmin |
            PermissionLevel.OrganizationCollaborator;
    }
}