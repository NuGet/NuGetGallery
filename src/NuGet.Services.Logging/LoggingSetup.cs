// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace NuGet.Services.Logging
{
    public static class LoggingSetup
    {
        public static LoggerConfiguration CreateDefaultLoggerConfiguration(bool withConsoleLogger = false)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.With(new MachineNameEnricher());

            if (withConsoleLogger)
            {
                loggerConfiguration = loggerConfiguration.WriteTo.ColoredConsole();
            }

            return loggerConfiguration;
        }

        public static ILoggerFactory CreateLoggerFactory(
            LoggerConfiguration loggerConfiguration = null,
            LogEventLevel applicationInsightsMinimumLogEventLevel = LogEventLevel.Information)
        {
            // setup Serilog
            if (loggerConfiguration == null)
            {
                loggerConfiguration = CreateDefaultLoggerConfiguration();
            }

            if (!string.IsNullOrEmpty(TelemetryConfiguration.Active.InstrumentationKey))
            {
                loggerConfiguration = loggerConfiguration.WriteTo.ApplicationInsights(
                    TelemetryConfiguration.Active.InstrumentationKey,
                    restrictedToMinimumLevel: applicationInsightsMinimumLogEventLevel);
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            // hook-up Serilog to Microsoft.Extensions.Logging
            var loggerFactory = new LoggerFactory();

            // note: this confusing setting will be removed when new version of Microsoft.Extensions.Logging is out
            // https://github.com/aspnet/Announcements/issues/122
            loggerFactory.MinimumLevel = LogLevel.Debug;
            loggerFactory.AddProvider(new SerilogLoggerProvider());

            // hook into anything that is being traced in other libs using system.diagnostics.trace
            System.Diagnostics.Trace.Listeners.Add(new SerilogTraceListener.SerilogTraceListener());

            return loggerFactory;
        }
    }
}
