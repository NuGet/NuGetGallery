// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// The interface for starting and stopping a background task which refreshes the cached feature flag state
    /// periodically.
    /// </summary>
    public interface IFeatureFlagRefresher
    {
        /// <summary>
        /// Load the initial feature flag state and start the feature flag refresh background task. If the refresher is
        /// already started, an exception will be thrown. If <see cref="FeatureFlagConfiguration.ConnectionString"/> is
        /// missing the nothing is done.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the refresher is already started.</exception>
        Task StartIfConfiguredAsync();

        /// <summary>
        /// Stops the refresh background task if it is started. This method waits for the refresh task to react to the
        /// cancellation so it may not return immediately. If the refresh background task is not started, this method
        /// does nothing.
        /// </summary>
        Task StopAndWaitAsync();
    }
}