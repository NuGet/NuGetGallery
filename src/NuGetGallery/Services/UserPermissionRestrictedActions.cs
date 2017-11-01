// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
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
    }
}