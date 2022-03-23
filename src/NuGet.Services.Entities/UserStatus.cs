// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    public enum UserStatus
    {
        /// <summary>
        /// The user is active and can perform all user operations. This does not consider additional requirements based on
        /// <see cref="User.Confirmed"/>, <see cref="User.FailedLoginCount"/>, or <see cref="User.IsDeleted"/>.
        /// </summary>
        Unlocked = 0,

        // enum value 1 is intentionally unused to allow future use for a "Deleted" status, to align with the
        // PackageStatus enum.

        /// <summary>
        /// The user is locked and is restricted from performing some operations.
        /// </summary>
        Locked = 2,
    }
}
