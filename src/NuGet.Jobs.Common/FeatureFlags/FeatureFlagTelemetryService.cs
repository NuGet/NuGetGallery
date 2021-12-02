// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.FeatureFlags;
using NuGet.Services.Logging;

namespace NuGet.Jobs
{
    public class FeatureFlagTelemetryService : IFeatureFlagTelemetryService
    {
        private const string FeatureFlagStalenessSeconds = "FeatureFlagStalenessSeconds";

        protected readonly ITelemetryClient _telemetryClient;

        public FeatureFlagTelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackFeatureFlagStaleness(TimeSpan staleness)
        {
            _telemetryClient.TrackMetric(
                FeatureFlagStalenessSeconds,
                staleness.TotalSeconds);
        }
    }
}
