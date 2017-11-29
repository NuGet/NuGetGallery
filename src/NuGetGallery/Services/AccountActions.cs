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
        /// If an account is requested to be an owner of a package, the user can accept the request on behalf of the account.
        /// </summary>
        public static PermissionsRequirement ManagePackageOwnershipOnBehalfOf =
            PermissionsRequirement.Owner |
            PermissionsRequirement.OrganizationAdmin;

        /// <summary>
        /// The user can see private information about an organization account.
        /// </summary>
        public static PermissionsRequirement DisplayPrivateOrganization =
            PermissionsRequirement.OrganizationAdmin |
            PermissionsRequirement.OrganizationCollaborator;
    }
}