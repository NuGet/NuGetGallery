// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace NuGet.Services.Logging
{
    /// <summary>
    /// Utility class to initialize an <see cref="ApplicationInsightsConfiguration"/> instance
    /// using provided instrumentation key, optional heartbeat interval, 
    /// and, if detected, taking into account an optional ApplicationInsights.config file.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="Initialize(string)"/> or <see cref="Initialize(string, TimeSpan)"/> returns the
    /// initialized <see cref="ApplicationInsightsConfiguration"/> object; 
    /// it does not set the obsolete <see cref="TelemetryClient.Active"/> component.
    /// 
    /// It is the caller's responsibility to ensure the returned configuration is used 
    /// when creating new <see cref="TelemetryClient"/> instances.
    /// </remarks>
    public static class ApplicationInsights
    {
        /// <summary>
        /// Initializes an <see cref="ApplicationInsightsConfiguration"/> using the provided
        /// <paramref name="instrumentationKey"/>, taking into account the <c>ApplicationInsights.config</c> file if present.
        /// </summary>
        /// <param name="instrumentationKey">The instrumentation key to use.</param>
        public static ApplicationInsightsConfiguration Initialize(string instrumentationKey)
        {
            return InitializeApplicationInsightsConfiguration(instrumentationKey, heartbeatInterval: null);
        }

        /// <summary>
        /// Initializes an <see cref="ApplicationInsightsConfiguration"/> using the provided
        /// <paramref name="instrumentationKey"/> and <paramref name="heartbeatInterval"/>, 
        /// taking into account the <c>ApplicationInsights.config</c> file if present.
        /// </summary>
        /// <param name="instrumentationKey">The instrumentation key to use.</param>
        /// <param name="heartbeatInterval">The heartbeat interval to use.</param>
        public static ApplicationInsightsConfiguration Initialize(
            string instrumentationKey,
            TimeSpan heartbeatInterval)
        {
            return InitializeApplicationInsightsConfiguration(instrumentationKey, heartbeatInterval);
        }

        private static ApplicationInsightsConfiguration InitializeApplicationInsightsConfiguration(
            string instrumentationKey,
            TimeSpan? heartbeatInterval)
        {
            // Note: TelemetryConfiguration.Active is being deprecated
            // https://github.com/microsoft/ApplicationInsights-dotnet/issues/1152
            // We use TelemetryConfiguration.CreateDefault() as opposed to instantiating a new TelemetryConfiguration()
            // to take into account the ApplicationInsights.config file (if detected).
            var telemetryConfiguration = TelemetryConfiguration.CreateDefault();

            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                telemetryConfiguration.InstrumentationKey = instrumentationKey;
            }

            telemetryConfiguration.TelemetryInitializers.Add(new TelemetryContextInitializer());

            // Construct a TelemetryClient to emit traces so we can track and debug AI initialization.
            var telemetryClient = new TelemetryClient(telemetryConfiguration);

            telemetryClient.TrackTrace(
                        $"TelemetryConfiguration initialized using instrumentation key: {instrumentationKey ?? "EMPTY"}.",
                        SeverityLevel.Information);

            var diagnosticsTelemetryModule = new DiagnosticsTelemetryModule();

            // Configure heartbeat interval if specified.
            // When not defined, the DiagnosticsTelemetryModule will use its internal defaults (heartbeat enabled, interval of 15 minutes).
            var traceMessage = "DiagnosticsTelemetryModule initialized using default heartbeat interval.";
            if (heartbeatInterval.HasValue)
            {
                diagnosticsTelemetryModule.HeartbeatInterval = heartbeatInterval.Value;
                traceMessage = $"DiagnosticsTelemetryModule initialized using configured heartbeat interval: {heartbeatInterval.Value}.";
            }

            diagnosticsTelemetryModule.Initialize(telemetryConfiguration);

            telemetryClient.TrackTrace(traceMessage, SeverityLevel.Information);

            return new ApplicationInsightsConfiguration(telemetryConfiguration, diagnosticsTelemetryModule);
        }
    }
}
