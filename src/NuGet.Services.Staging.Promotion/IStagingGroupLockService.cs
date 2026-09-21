// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Serializes staging group state transitions within the caller's database transaction.
    /// </summary>
    public interface IStagingGroupLockService
    {
        /// <summary>
        /// Acquires an update lock on a staging group until the current transaction completes.
        /// </summary>
        /// <param name="stagingGroupKey">The staging group key.</param>
        /// <returns>A task that represents the lock acquisition.</returns>
        Task AcquireAsync(int stagingGroupKey);
    }
}
