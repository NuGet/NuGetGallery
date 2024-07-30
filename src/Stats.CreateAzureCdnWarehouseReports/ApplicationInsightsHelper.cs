// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal sealed class ApplicationInsightsHelper
    {
        private readonly TelemetryClient _telemetryClient;

        public ApplicationInsightsHelper(TelemetryConfiguration telemetryConfiguration)
        {
            if (telemetryConfiguration == null)
            {
                throw new ArgumentNullException(nameof(telemetryConfiguration));
            }

            _telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        public void TrackReportProcessed(string reportName, string packageId = null)
        {
            var telemetry = new MetricTelemetry(reportName, 1);

            if (!string.IsNullOrWhiteSpace(packageId))
            {
                telemetry.Properties.Add("Package Id", packageId);
            }

            _telemetryClient.TrackMetric(telemetry);
            _telemetryClient.Flush();
        }

        public void TrackMetric(string metricName, double value, string logFileName = null)
        {
            var telemetry = new MetricTelemetry(metricName, value);

            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            _telemetryClient.TrackMetric(telemetry);
            _telemetryClient.Flush();
        }
    }
}