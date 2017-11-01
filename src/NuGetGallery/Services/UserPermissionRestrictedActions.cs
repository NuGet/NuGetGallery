// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// <see cref="IPermissionRestrictedAction"/>s that operate on a <see cref="User"/>.
    /// </summary>
    public static class UserPermissionRestrictedActions
    {
        /// <summary>
        /// If a user is requested to be an owner of a package, this user can accept the request on behalf of the other user.
        /// </summary>
        public static IPermissionRestrictedAction AcceptPackageOwnershipOnBehalfOf =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
            });

        /// <summary>
        /// The user can see private information about an organization user.
        /// </summary>
        public static IPermissionRestrictedAction DisplayPrivateOrganization =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.OrganizationAdmin,
                PermissionLevel.OrganizationCollaborator,
            });
    }
}