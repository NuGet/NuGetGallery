// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public enum AuditedPackageRegistrationAction
    {
        AddOwner,
        RemoveOwner,
        MarkVerified,
        MarkUnverified,
        SetRequiredSigner,
        AddOwnershipRequest,
        DeleteOwnershipRequest,
        Lock,
        Unlock,
    }
}