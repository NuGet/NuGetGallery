// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ILockUserService
    {
        /// <summary>
        /// Sets the lock state of a user.
        /// </summary>
        /// <param name="username">The username of the user to lock or unlock.</param>
        /// <param name="isLocked">True to lock, false to unlock.</param>
        /// <returns>The result of the operation.</returns>
        Task<LockUserServiceResult> SetLockStateAsync(string username, bool isLocked, string reason = null, string callerIdentity = null);
    }

    public enum LockUserServiceResult
    {
        Success,
        UserNotFound,
    }
}
