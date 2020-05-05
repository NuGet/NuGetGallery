// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.FeatureFlags;
using NuGet.Services.Logging;

namespace NuGet.Services.V3
{
    public class V3TelemetryService : IV3TelemetryService, IFeatureFlagTelemetryService
    {
        private const string Prefix = "V3.";

        private readonly ITelemetryClient _telemetryClient;

        public V3TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public IDisposable TrackCatalogLeafDownloadBatch(int count)
        {
            return _telemetryClient.TrackDuration(
                Prefix + "CatalogLeafDownloadBatchSeconds",
                new Dictionary<string, string>
                {
                    { "Count", count.ToString() },
                });
        }

        public void TrackFeatureFlagStaleness(TimeSpan staleness)
        {
            _telemetryClient.TrackMetric(
                Prefix + "FeatureFlagStalenessSeconds",
                staleness.TotalSeconds);
        }
    }
}
