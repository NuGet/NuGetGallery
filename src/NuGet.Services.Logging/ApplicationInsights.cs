// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    public static class ApplicationInsights
    {
        public static bool Initialized { get; private set; }

        public static void Initialize(string instrumentationKey)
        {
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;
                TelemetryConfiguration.Active.TelemetryInitializers.Add(new TelemetryContextInitializer());

                Initialized = true;
            }
            else
            {
                Initialized = false;
            }
        }

        public static void TrackException(Exception exception)
        {
            if (!Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            var telemetry = new ExceptionTelemetry(exception);

            telemetryClient.TrackException(telemetry);
            telemetryClient.Flush();
        }

        public static void TrackTrace(string message)
        {
            if (!Initialized)
            {
                return;
            }

            var telemetryClient = new TelemetryClient();
            telemetryClient.TrackTrace(message);
            telemetryClient.Flush();
        }
    }
}