// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using NuGet.Jobs;
using NuGet.Services.Logging;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal static class ApplicationInsightsHelper
    {
        public static void TrackReportProcessed(string reportName, string packageId = null)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new MetricTelemetry(reportName, 1);

            if (!string.IsNullOrWhiteSpace(packageId))
            {
                telemetry.Properties.Add("Package Id", packageId);
            }

            telemetryClient.TrackMetric(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackMetric(string metricName, double value, string logFileName = null)
        {
            if (!ApplicationInsights.Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new MetricTelemetry(metricName, value);

            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            telemetryClient.TrackMetric(telemetry);
            telemetryClient.Flush();
        }
    }
}