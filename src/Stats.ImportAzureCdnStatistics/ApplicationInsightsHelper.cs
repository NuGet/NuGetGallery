// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Stats.ImportAzureCdnStatistics
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

        public void TrackException(Exception exception, string logFileName = null, string message = null)
        {
            var telemetry = new ExceptionTelemetry(exception);

            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                telemetry.Properties.Add("Message", message);
            }

            _telemetryClient.TrackException(telemetry);
            _telemetryClient.Flush();
        }

        public void TrackSqlException(string eventName, SqlException sqlException, string logFileName, string dimension)
        {
            var telemetry = new EventTelemetry(eventName);
            telemetry.Properties.Add("Dimension", dimension);
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            _telemetryClient.TrackEvent(telemetry);
            _telemetryClient.Flush();

            TrackException(sqlException, logFileName);
        }

        public void TrackToolNotFound(string id, string version, string fileName, string logFileName)
        {
            var telemetry = new EventTelemetry("ToolNotFound");
            telemetry.Properties.Add("ToolId", id);
            telemetry.Properties.Add("ToolVersion", version);
            telemetry.Properties.Add("FileName", fileName);
            telemetry.Properties.Add("LogFile", logFileName);

            _telemetryClient.TrackEvent(telemetry);
            _telemetryClient.Flush();
        }

        public void TrackRetrieveDimensionDuration(string dimension, long value, string logFileName)
        {
            var telemetry = new MetricTelemetry("Retrieve Dimension duration (ms)", value);
            telemetry.Properties.Add("Dimension", dimension);
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            _telemetryClient.TrackMetric(telemetry);
            _telemetryClient.Flush();
        }

        public void TrackPackageNotFound(string id, string version, string logFileName)
        {
            var telemetry = new EventTelemetry("PackageNotFound");
            telemetry.Properties.Add("PackageId", id);
            telemetry.Properties.Add("PackageVersion", version);
            telemetry.Properties.Add("LogFile", logFileName);

            _telemetryClient.TrackEvent(telemetry);
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