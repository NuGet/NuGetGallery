// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace NuGet.Services.Logging
{
    public sealed class ApplicationInsightsConfiguration
    {
        internal ApplicationInsightsConfiguration(
            TelemetryConfiguration telemetryConfiguration,
            DiagnosticsTelemetryModule diagnosticsTelemetryModule)
        {
            TelemetryConfiguration = telemetryConfiguration ?? throw new ArgumentNullException(nameof(telemetryConfiguration));
            DiagnosticsTelemetryModule = diagnosticsTelemetryModule ?? throw new ArgumentNullException(nameof(diagnosticsTelemetryModule));
        }

        /// <summary>
        /// Contains the initialized <see cref="Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration"/>.
        /// Used to initialize new <see cref="Microsoft.ApplicationInsights.TelemetryClient"/> instances.
        /// Allows tweaking telemetry initializers.
        /// </summary>
        /// <remarks>
        /// Needs to be disposed when gracefully shutting down the application.
        /// </remarks>
        public TelemetryConfiguration TelemetryConfiguration { get; }

        /// <summary>
        /// Contains the initialized <see cref="Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing.DiagnosticsTelemetryModule"/>.
        /// Allows tweaking Application Insights heartbeat telemetry.
        /// </summary>
        public DiagnosticsTelemetryModule DiagnosticsTelemetryModule { get; }
    }
}
