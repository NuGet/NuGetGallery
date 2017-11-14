// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class ReservedNamespaceActions
    {
        /// <summary>
        /// The user can push to this reserved namespace.
        /// </summary>
        public static PermissionLevel PushToReservedNamespace =
            PermissionLevel.Owner |
            PermissionLevel.SiteAdmin;
    }
}