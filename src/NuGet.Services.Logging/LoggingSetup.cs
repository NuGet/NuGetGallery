// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace NuGet.Services.Logging
{
    public static class LoggingSetup
    {
        public static LoggerConfiguration CreateDefaultLoggerConfiguration(bool withConsoleLogger = false)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose();

            loggerConfiguration.Enrich.WithMachineName();
            loggerConfiguration.Enrich.WithProcessId();
            loggerConfiguration.Enrich.FromLogContext();

            if (withConsoleLogger)
            {
                loggerConfiguration = loggerConfiguration.WriteTo.ColoredConsole();
            }

            return loggerConfiguration;
        }

        public static ILoggerFactory CreateLoggerFactory(
            LoggerConfiguration loggerConfiguration = null,
            LogEventLevel applicationInsightsMinimumLogEventLevel = LogEventLevel.Information,
            TelemetryConfiguration telemetryConfiguration = null)
        {
            // setup Serilog
            if (loggerConfiguration == null)
            {
                loggerConfiguration = CreateDefaultLoggerConfiguration();
            }

            if (telemetryConfiguration != null 
                && !string.IsNullOrEmpty(telemetryConfiguration.InstrumentationKey))
            {
                // Even though this method call is marked [Obsolete],
                // there's currently no other way to pass in the active TelemetryConfiguration as configured in DI.
                // These SeriLog APIs are very likely to change to support passing in the TelemetryConfiguration again.
                // See also https://github.com/serilog/serilog-sinks-applicationinsights/issues/121.

#pragma warning disable CS0618 // Type or member is obsolete
                loggerConfiguration = loggerConfiguration.WriteTo.ApplicationInsights(
                    telemetryConfiguration, 
                    applicationInsightsMinimumLogEventLevel);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            // hook-up Serilog to Microsoft.Extensions.Logging
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog();

            if (!Trace.Listeners.OfType<SerilogTraceListener.SerilogTraceListener>().Any())
            {
                // hook into anything that is being traced in other libs using system.diagnostics.trace
                Trace.Listeners.Add(new SerilogTraceListener.SerilogTraceListener());
            }

            return loggerFactory;
        }
    }
}