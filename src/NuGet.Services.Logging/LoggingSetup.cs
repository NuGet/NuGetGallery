// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace NuGet.Services.Logging
{
    public static class LoggingSetup
    {
        public static LoggerConfiguration CreateDefaultLoggerConfiguration(
            bool withConsoleLogger = false,
            bool withAssemblyMetadata = true,
            Assembly metadataSourceAssembly = null)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose();

            loggerConfiguration.Enrich.WithMachineName();
            loggerConfiguration.Enrich.WithProcessId();
            loggerConfiguration.Enrich.FromLogContext();
            if (withAssemblyMetadata)
            {
                metadataSourceAssembly ??= Assembly.GetEntryAssembly();
                if (metadataSourceAssembly != null)
                {
                    loggerConfiguration.Enrich.WithNuGetAssemblyMetadata(metadataSourceAssembly);
                }
            }

            if (withConsoleLogger)
            {
                loggerConfiguration = loggerConfiguration.WriteTo.Console();
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
                loggerConfiguration = loggerConfiguration.WriteTo.ApplicationInsights(
                    telemetryConfiguration,
                    TelemetryConverter.Traces,
                    applicationInsightsMinimumLogEventLevel);
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
