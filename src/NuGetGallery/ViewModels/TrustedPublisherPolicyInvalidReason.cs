// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    // NOTE that we use positive numbers for enum values to simplicy JavaScript interop.
    public enum TrustedPublisherPolicyInvalidReason
    {
        /// <summary>
        /// User owning a policy is not a member of organization owning a policy.
        /// </summary>
        UserNotInOrganization = 1,

        /// <summary>
        /// Organization owning a policy is locked or deleted.
        /// </summary>
        OrganizationIsLockedOrDeleted = 2,
    }
}
