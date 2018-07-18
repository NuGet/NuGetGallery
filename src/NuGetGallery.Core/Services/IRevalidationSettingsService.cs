// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IRevalidationSettingsService
    {
        /// <summary>
        /// Get the latest settings. Throws if the settings blob cannot be found.
        /// </summary>
        /// <returns>The latest settings.</returns>
        Task<RevalidationSettings> GetSettingsAsync();

        /// <summary>
        /// Attempt to update the latest settings.
        /// </summary>
        /// <param name="updateAction">The action used to update the settings.</param>
        /// <returns>A task that completes once the settings have been updated.</returns>
        Task UpdateSettingsAsync(Action<RevalidationSettings> updateAction);

        /// <summary>
        /// Attempt to update the latest settings.
        /// </summary>
        /// <param name="updateAction">The callback that updates the settings. Changes are only persisted if the callback returns true</param>
        /// <returns>The updated settings.</returns>
        Task<RevalidationSettings> MaybeUpdateSettingsAsync(Func<RevalidationSettings, bool> updateAction);
    }
}
