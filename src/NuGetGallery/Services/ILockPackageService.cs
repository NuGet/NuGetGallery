// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ILockPackageService
    {
        /// <summary>
        /// Sets the lock state of a package registration.
        /// </summary>
        /// <param name="packageId">The package ID whose registration to lock or unlock.</param>
        /// <param name="isLocked">True to lock, false to unlock.</param>
        /// <returns>The result of the operation.</returns>
        Task<LockPackageServiceResult> SetLockStateAsync(string packageId, bool isLocked, string reason = null, string callerIdentity = null);
    }

    public enum LockPackageServiceResult
    {
        Success,
        PackageNotFound,
    }
}
