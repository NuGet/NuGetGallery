// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.FeatureFlags
{
    /// <summary>
    /// The service that caches and periodically refreshes the feature flags' state.
    /// </summary>
    public interface IFeatureFlagCacheService
    {
        /// <summary>
        /// Continuously refresh the feature flags' cache.
        /// </summary>
        /// <param name="cancellationToken">Cancelling this token will complete the returned task.</param>
        /// <returns>A task that completes once the cancellation token is cancelled.</returns>
        Task RunAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Refresh the feature flags cache once.
        /// </summary>
        /// <remarks>This should be called at app startup to guarantee feature flags have been loaded.</remarks>
        /// <returns>A task that completes once the cached feature flags have been refreshed.</returns>
        Task RefreshAsync();

        /// <summary>
        /// Fetch the latest cached flags. This should be called after either <see cref="RunAsync(CancellationToken)"/>
        /// or <see cref="RefreshAsync(CancellationToken)"/>.
        /// </summary>
        /// <returns>The latest cached flags, or null if the flags have never been loaded successfully.</returns>
        FeatureFlags GetLatestFlagsOrNull();

        /// <summary>
        /// Fetch the time at which the flags were last refreshed.
        /// </summary>
        /// <returns>The last time flags were refreshed, or null if the flags have never been loaded successfully.</returns>
        DateTimeOffset? GetRefreshTimeOrNull();
    }
}
