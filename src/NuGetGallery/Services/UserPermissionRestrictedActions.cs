// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class UserPermissionRestrictedActions
    {
        public static IPermissionRestrictedAction AcceptOwnershipOnBehalfOf =
            new PermissionRestrictedActionIncludeLevel(new PermissionLevel[]
            {
                PermissionLevel.Owner,
                PermissionLevel.OrganizationAdmin,
            });
    }
}