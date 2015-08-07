// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Stats.AzureCdnLogs.Common
{
    public static class ApplicationInsights
    {
        private static bool _initialized;

        public static void Initialize(string instrumentationKey)
        {
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;
                TelemetryConfiguration.Active.ContextInitializers.Add(new SessionInitializer());

                _initialized = true;
            }
            else
            {
                _initialized = false;
            }
        }

        public static void TrackException(Exception exception, string logFileName = null)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new ExceptionTelemetry(exception);

            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            telemetryClient.TrackException(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackPackageNotFound(string id, string version, string logFileName)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new EventTelemetry("PackageNotFound");
            telemetry.Properties.Add("PackageId", id);
            telemetry.Properties.Add("PackageVersion", version);
            telemetry.Properties.Add("LogFile", logFileName);

            telemetryClient.TrackEvent(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackMetric(string metricName, double value, string logFileName)
        {
        }

        public static void TrackRetrieveDimensionDuration(string dimension, long value, string logFileName)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new MetricTelemetry("Retrieve Dimension duration (ms)", value);
            telemetry.Properties.Add("Dimension", dimension);
            telemetry.Properties.Add("LogFile", logFileName);

            telemetryClient.TrackMetric(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackSqlException(string eventName, SqlException sqlException, string logFileName, string dimension)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new EventTelemetry(eventName);
            telemetry.Properties.Add("Dimension", dimension);
            telemetry.Properties.Add("LogFile", logFileName);

            telemetryClient.TrackEvent(telemetry);
            telemetryClient.Flush();

            TrackException(sqlException, logFileName);
        }
    }
}