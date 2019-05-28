// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace NuGetGallery.Services.Telemetry
{
    /// <summary>
    /// Wrapper for the Application Insights TelemetryClient class.
    /// </summary>
    public class TelemetryClientWrapper : ITelemetryClient
    {
        private static Lazy<TelemetryClientWrapper> Singleton = new Lazy<TelemetryClientWrapper>(() => new TelemetryClientWrapper());

        public static TelemetryClientWrapper Instance
        {
            get
            {
                return Singleton.Value;
            }
        }

        private TelemetryClientWrapper()
        {
            UnderlyingClient = new TelemetryClient();
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
    }
}