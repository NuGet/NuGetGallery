// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using NuGet.Services.Logging;

namespace Stats.RollUpDownloadFacts
{
    internal static class ApplicationInsightsHelper
    {
        public static void TrackRollUpMetric(string metricName, double value, string packageDimensionId)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new MetricTelemetry(metricName, value);
            telemetry.Properties.Add("PackageDimensionId", packageDimensionId);

            telemetryClient.TrackMetric(telemetry);
            telemetryClient.Flush();
        }
    }
}