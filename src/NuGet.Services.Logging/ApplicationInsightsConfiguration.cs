// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace NuGet.Services.Logging
{
    public sealed class ApplicationInsightsConfiguration
        : IDisposable
    {
        internal ApplicationInsightsConfiguration(
            TelemetryConfiguration telemetryConfiguration,
            DiagnosticsTelemetryModule diagnosticsTelemetryModule,
            DependencyTrackingTelemetryModule dependencyTrackingTelemetryModule = null)
        {
            TelemetryConfiguration = telemetryConfiguration ?? throw new ArgumentNullException(nameof(telemetryConfiguration));
            DiagnosticsTelemetryModule = diagnosticsTelemetryModule ?? throw new ArgumentNullException(nameof(diagnosticsTelemetryModule));
            DependencyTrackingTelemetryModule = dependencyTrackingTelemetryModule;
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

        /// <summary>
        /// Contains the initialized <see cref="Microsoft.ApplicationInsights.DependencyCollector.DependencyTrackingTelemetryModule"/>,
        /// or <c>null</c> when dependency tracking was not enabled during initialization.
        /// When set, outbound dependency calls (HTTP, SQL, etc.) are collected into the Application Insights <c>dependencies</c> table.
        /// </summary>
        public DependencyTrackingTelemetryModule DependencyTrackingTelemetryModule { get; }

        public void Dispose()
        {
            DependencyTrackingTelemetryModule?.Dispose();
            DiagnosticsTelemetryModule.Dispose();
            TelemetryConfiguration.Dispose();
        }
    }
}
