// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace NuGetGallery
{
    /// <summary>
    /// Wrapper for the Application Insights TelemetryClient class.
    /// </summary>
    public class TelemetryClientWrapper : ITelemetryClient
    {
        private const string TelemetryPropertyEventId = "EventId";
        private const string TelemetryPropertyEventName = "EventName";

        public static TelemetryClientWrapper UseTelemetryConfiguration(TelemetryConfiguration configuration)
        {
            return new TelemetryClientWrapper(configuration);
        }

        private TelemetryClientWrapper(TelemetryConfiguration telemetryConfiguration)
        {
            UnderlyingClient = new TelemetryClient(telemetryConfiguration)
            {
                InstrumentationKey = telemetryConfiguration.InstrumentationKey
            };
        }

        public TelemetryClient UnderlyingClient { get; }

        public void TrackException(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                UnderlyingClient.TrackException(exception, properties, metrics);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public void TrackMetric(string metricName, double value, IDictionary<string, string> properties = null)
        {
            try
            {
                UnderlyingClient.TrackMetric(metricName, value, properties);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public void TrackDependency(string dependencyTypeName,
                                    string target,
                                    string dependencyName,
                                    string data,
                                    DateTimeOffset startTime,
                                    TimeSpan duration,
                                    string resultCode,
                                    bool success,
                                    IDictionary<string, string> properties)
        {
            try
            {
                var telemetry = new DependencyTelemetry(dependencyTypeName, target, dependencyName, data, startTime, duration, resultCode, success);
                foreach (var property in properties)
                {
                    telemetry.Properties.Add(property);
                }

                UnderlyingClient.TrackDependency(telemetry);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        public void TrackTrace(string message, LogLevel logLevel, EventId eventId)
        {
            try
            {
                var telemetry = new TraceTelemetry(
                    message,
                    LogLevelToSeverityLevel(logLevel));

                telemetry.Properties[TelemetryPropertyEventId] = eventId.Id.ToString();

                if (!string.IsNullOrWhiteSpace(eventId.Name))
                {
                    telemetry.Properties[TelemetryPropertyEventName] = eventId.Name;
                }

                UnderlyingClient.TrackTrace(telemetry);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }

        private static SeverityLevel LogLevelToSeverityLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical: return SeverityLevel.Critical;
                case LogLevel.Error: return SeverityLevel.Error;
                case LogLevel.Warning: return SeverityLevel.Warning;
                case LogLevel.Information: return SeverityLevel.Information;
                case LogLevel.Trace:
                default: return SeverityLevel.Verbose;
            }
        }
    }
}