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
    public static class Logging
    {
        public static LoggerConfiguration CreateDefaultLoggerConfiguration()
        {
            var defaultLoggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.With(new MachineNameEnricher());

#if DEBUG
            defaultLoggerConfiguration = defaultLoggerConfiguration.WriteTo.ColoredConsole();
#endif

            return defaultLoggerConfiguration;
        }

        public static ILoggerFactory CreateLoggerFactory(LoggerConfiguration loggerConfiguration = null)
        {
            // setup Serilog
            if (loggerConfiguration == null)
            {
                loggerConfiguration = CreateDefaultLoggerConfiguration();
            }

            if (!string.IsNullOrEmpty(TelemetryConfiguration.Active.InstrumentationKey))
            {
                loggerConfiguration = loggerConfiguration.WriteTo
                    .ApplicationInsights(TelemetryConfiguration.Active.InstrumentationKey,
                    restrictedToMinimumLevel: LogEventLevel.Information);
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
