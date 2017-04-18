// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public enum UserSecurityPolicyAuditAction
    {
        /// <summary>
        /// User subscribed to the package verification keys policy.
        /// </summary>
        PackageVerificationKeysPolicy_AddPolicy,

        /// <summary>
        /// The package verification keys policy was evaluated for a package push.
        /// </summary>
        PackageVerificationKeysPolicy_CreatePackage,

        /// <summary>
        /// The package verification keys policy was evaluated for a package push verification.
        /// </summary>
        PackageVerificationKeysPolicy_VerifyPackageKey,
    }
}
