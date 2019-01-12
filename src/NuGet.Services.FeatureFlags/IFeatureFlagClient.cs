// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.FeatureFlags
{
    public interface IFeatureFlagClient
    {
        /// <summary>
        /// Get whether a feature is enabled. This method does not throw.
        /// </summary>
        /// <param name="feature">The unique identifier for this feature. This is case insensitive.</param>
        /// <param name="defaultValue">The value to return if the status of the feature is unknown.</param>
        /// <returns>Whether the feature is enabled.</returns>
        bool IsEnabled(string feature, bool defaultValue);

        /// <summary>
        /// Get whether a flight is enabled for a user. This method does not throw.
        /// </summary>
        /// <param name="flight">The unique identifier for this flight. This is case insensitive.</param>
        /// <param name="user">The user whose status should be determined.</param>
        /// <param name="defaultValue">The value to return if the status of the flight is unknown.</param>
        /// <returns>Whether the flight is enabled for this user.</returns>
        bool IsEnabled(string flight, IFlightUser user, bool defaultValue);
    }
}
