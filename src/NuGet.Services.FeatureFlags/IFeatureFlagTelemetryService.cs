// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.FeatureFlags
{
    public interface IFeatureFlagTelemetryService
    {
        /// <summary>
        /// Track the time since the feature flags were last refreshed successfully.
        /// </summary>
        /// <param name="staleness">The time since the flags were last refreshed successfully.</param>
        void TrackFeatureFlagStaleness(TimeSpan staleness);
    }
}
