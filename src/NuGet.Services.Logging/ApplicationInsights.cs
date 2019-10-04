// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace NuGet.Services.Logging
{
    public static class ApplicationInsights
    {
        public static IHeartbeatPropertyManager HeartbeatManager { get; private set; }

        public static bool Initialized { get; private set; }

        public static void Initialize(string instrumentationKey)
        {
            InitializeTelemetryConfiguration(instrumentationKey, heartbeatInterval: null);
        }

        public static void Initialize(string instrumentationKey, TimeSpan heartbeatInterval)
        {
            InitializeTelemetryConfiguration(instrumentationKey, heartbeatInterval);
        }

        private static void InitializeTelemetryConfiguration(string instrumentationKey, TimeSpan? heartbeatInterval)
        {
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;
                TelemetryConfiguration.Active.TelemetryInitializers.Add(new TelemetryContextInitializer());

                // Configure heartbeat interval if specified.
                // When not defined or null, the DiagnosticsTelemetryModule will use its internal defaults (heartbeat enabled, interval of 15 minutes).
                if (heartbeatInterval.HasValue)
                {
                    var heartbeatManager = GetHeartbeatPropertyManager();
                    if (heartbeatManager != null)
                    {
                        heartbeatManager.HeartbeatInterval = heartbeatInterval.Value;

                        Trace.TraceInformation($"Telemetry initialized using configured heartbeat interval: {heartbeatInterval.Value}.");
                    }
                }
                else
                {
                    Trace.TraceInformation($"Telemetry initialized using default heartbeat interval.");
                }

                Initialized = true;
            }
            else
            {
                Initialized = false;
            }
        }

        private static IHeartbeatPropertyManager GetHeartbeatPropertyManager()
        {
            if (HeartbeatManager == null)
            {
                var telemetryModules = TelemetryModules.Instance;

                try
                {
                    foreach (var module in telemetryModules.Modules)
                    {
                        if (module is IHeartbeatPropertyManager heartbeatManager)
                        {
                            HeartbeatManager = heartbeatManager;
                        }
                    }
                }
                catch (Exception hearbeatManagerAccessException)
                {
                    // An non-critical, unexpected exception occurred trying to access the heartbeat manager.
                    Trace.TraceError($"There was an error accessing heartbeat manager. Details: {hearbeatManagerAccessException.ToInvariantString()}");
                }

                if (HeartbeatManager == null)
                {
                    // Heartbeat manager unavailable: log warning.
                    Trace.TraceWarning("Heartbeat manager unavailable");
                }
            }

            return HeartbeatManager;
        }
    }
}
