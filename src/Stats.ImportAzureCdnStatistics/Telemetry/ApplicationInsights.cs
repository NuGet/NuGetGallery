// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Stats.ImportAzureCdnStatistics
{
    internal static class ApplicationInsights
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

        public static void TrackException(Exception exception)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var exceptionTelemetry = new ExceptionTelemetry(exception);
            telemetryClient.TrackException(exceptionTelemetry);
            telemetryClient.Flush();
        }

        public static void TrackPackageNotFound(string id, string version)
        {
            if (!_initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var eventTelemetry = new EventTelemetry("PackageNotFound");
            eventTelemetry.Properties.Add("PackageId", id);
            eventTelemetry.Properties.Add("PackageVersion", version);

            telemetryClient.TrackEvent(eventTelemetry);
            telemetryClient.Flush();
        }
    }
}