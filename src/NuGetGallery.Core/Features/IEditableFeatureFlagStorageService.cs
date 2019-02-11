// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public interface IEditableFeatureFlagStorageService : IFeatureFlagStorageService
    {
        /// <summary>
        /// Get a reference to the feature flag's raw content.  This should be used
        /// in conjuction with <see cref="TrySaveAsync(string, string)"/> to update
        /// the feature flags.
        /// </summary>
        /// <returns>A snapshot of the flags' content and ETag.</returns>
        Task<FeatureFlagReference> GetReferenceAsync();

        /// <summary>
        /// Try to update the feature flags.
        /// </summary>
        /// <param name="flags">The feature flags serialized in JSON.</param>
        /// <param name="contentId">The feature flag's ETag.</param>
        /// <returns>The result of the save operation.</returns>
        Task<FeatureFlagSaveResult> TrySaveAsync(string flags, string contentId);

        /// <summary>
        /// Remove the user from the feature flags if needed.
        /// </summary>
        /// <param name="user">The user to remove from feature flags.</param>
        /// <returns>False if removing the user failed.</returns>
        Task<bool> TryRemoveUserAsync(User user);
    }
}
