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

        public void TrackReportProcessed(string reportName)
        {
            var telemetry = new MetricTelemetry(reportName, 1);

            _telemetryClient.TrackMetric(telemetry);
            _telemetryClient.Flush();
        }

        public void TrackMetric(string metricName, double value)
        {
            var telemetry = new MetricTelemetry(metricName, value);

            _telemetryClient.TrackMetric(telemetry);
            _telemetryClient.Flush();
        }
    }
}
