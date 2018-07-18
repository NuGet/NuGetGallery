// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IRevalidationStateService
    {
        /// <summary>
        /// Get the latest state. Throws if the state blob cannot be found.
        /// </summary>
        /// <returns>The latest state.</returns>
        Task<RevalidationState> GetStateAsync();

        /// <summary>
        /// Attempt to update the state atomically. Throws if the update fails.
        /// </summary>
        /// <param name="updateAction">The action used to update the state.</param>
        /// <returns>A task that completes once the state has been updated.</returns>
        Task UpdateStateAsync(Action<RevalidationState> updateAction);

        /// <summary>
        /// Attempt to update the state atomically. Throws is the update fails.
        /// </summary>
        /// <param name="updateAction">The callback that updates the state. Changes are only persisted if the callback returns true</param>
        /// <returns>The updated state.</returns>
        Task<RevalidationState> MaybeUpdateStateAsync(Func<RevalidationState, bool> updateAction);
    }
}
