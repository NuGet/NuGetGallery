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

        public static void TrackException(Exception exception, string logFileName = null, string message = null)
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

            if (!string.IsNullOrWhiteSpace(message))
            {
                telemetry.Properties.Add("Message", message);
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

        public static void TrackToolNotFound(string id, string version, string fileName, string logFileName)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new EventTelemetry("ToolNotFound");
            telemetry.Properties.Add("ToolId", id);
            telemetry.Properties.Add("ToolVersion", version);
            telemetry.Properties.Add("FileName", fileName);
            telemetry.Properties.Add("LogFile", logFileName);

            telemetryClient.TrackEvent(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackMetric(string metricName, double value, string logFileName = null)
        {
            if (!_initialized)
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

        public static void TrackRollUpMetric(string metricName, double value, string packageDimensionId)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new MetricTelemetry(metricName, value);
            telemetry.Properties.Add("PackageDimensionId", packageDimensionId);

            telemetryClient.TrackMetric(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackReportProcessed(string reportName, string packageId = null)
        {
            if (!_initialized)
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

        public static void TrackRetrieveDimensionDuration(string dimension, long value, string logFileName)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new MetricTelemetry("Retrieve Dimension duration (ms)", value);
            telemetry.Properties.Add("Dimension", dimension);
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

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
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                telemetry.Properties.Add("LogFile", logFileName);
            }

            telemetryClient.TrackEvent(telemetry);
            telemetryClient.Flush();

            TrackException(sqlException, logFileName);
        }

        public static void TrackTrace(string message)
        {
            var telemetryClient = new TelemetryClient();
            telemetryClient.TrackTrace(message);
            telemetryClient.Flush();
        }
    }
}