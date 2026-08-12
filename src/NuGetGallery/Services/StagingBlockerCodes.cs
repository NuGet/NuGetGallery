// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Stable <c>blockers[].code</c> values describing why a staged package cannot currently be promoted.
    /// This namespace is distinct from HTTP <see cref="StagingApiErrorCodes"/> and from artifact validation error codes.
    /// </summary>
    public static class StagingBlockerCodes
    {
        public const string GroupedPackage = "GroupedPackage";
        public const string PackageValidationFailed = "PackageValidationFailed";
        public const string SymbolValidationFailed = "SymbolValidationFailed";
        public const string ParentValidating = "ParentValidating";
        public const string ParentValidationFailed = "ParentValidationFailed";
        public const string ParentDeleted = "ParentDeleted";
        public const string ParentNotFound = "ParentNotFound";
        public const string OwnershipRemoved = "OwnershipRemoved";
        public const string PromotionFailed = "PromotionFailed";
    }
}
