// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Logging;

namespace Stats.CollectAzureCdnLogs
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "Stats.CollectAzureCdnLogs.";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackRawLogCount(int count)
        {
            _telemetryClient.TrackMetric(Prefix + "RawLogCount", count);
        }
    }
}
