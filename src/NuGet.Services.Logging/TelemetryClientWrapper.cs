// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace NuGet.Services.Logging
{
    public class TelemetryClientWrapper : ITelemetryClient
    {
        private readonly TelemetryClient _telemetryClient;

        public TelemetryClientWrapper(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void TrackException(
            Exception exception,
            IDictionary<string, string> properties = null,
            IDictionary<string, double> metrics = null)
        {
            try
            {
                _telemetryClient.TrackException(exception, properties, metrics);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public void TrackMetric(
            string metricName,
            double value,
            IDictionary<string, string> properties = null)
        {
            try
            {
                _telemetryClient.TrackMetric(metricName, value, properties);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public void TrackMetric(
            DateTimeOffset timestamp,
            string metricName,
            double value,
            IDictionary<string, string> properties = null)
        {
            try
            {
                var metricTelemetry = new MetricTelemetry
                {
                    Timestamp = timestamp,
                    Name = metricName,
                    Value = value
                };

                if (properties != null)
                {
                    foreach (var key in properties.Keys)
                    {
                        metricTelemetry.Properties.Add(key, properties[key]);
                    }
                }

                _telemetryClient.TrackMetric(metricTelemetry);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}