// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public enum AuditedAuthenticatedOperationAction
    {
        /// <summary>
        /// Package push was attempted by a non-owner of the package
        /// </summary>
        PackagePushAttemptByNonOwner,

        /// <summary>
        /// Login failed, no such user
        /// </summary>
        FailedLoginNoSuchUser,

        /// <summary>
        /// Login failed, user exists but password is invalid
        /// </summary>
        FailedLoginInvalidPassword,

        /// <summary>
        /// Login failed, user is an organization and should not have credentials.
        /// </summary>
        FailedLoginUserIsOrganization,

        /// <summary>
        /// Symbol package push was attempted by a non-owner of the package
        /// </summary>
        SymbolsPackagePushAttemptByNonOwner
    }
}