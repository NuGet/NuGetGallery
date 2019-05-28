// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services.AccountManagement
{
    public enum AccountDeletionOrphanPackagePolicy
    {
        /// <summary>
        /// Any orphan packages created by deleting the account should remain listed.
        /// </summary>
        KeepOrphans,

        /// <summary>
        /// Any orphan packages created by deleting the account should be unlisted.
        /// </summary>
        UnlistOrphans,

        /// <summary>
        /// Deleting the account should not create any orphan packages.
        /// </summary>
        DoNotAllowOrphans,
    }
}