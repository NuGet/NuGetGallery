// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Logging;

namespace Search.GenerateAuxiliaryData.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        private const string Prefix = "Search.GenerateAuxiliaryData.";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        public void TrackExporterDuration(string exporter, string report, TimeSpan duration, bool success)
        {
            _telemetryClient.TrackMetric(
                Prefix + "ExporterDurationMs",
                duration.TotalMilliseconds,
                new Dictionary<string, string>
                {
                    { "Exporter", exporter },
                    { "Report", report },
                    { "Success", success.ToString() },
                });
        }
    }
}
