// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;
using Serilog.Context;
using Serilog.Events;
using System.Threading.Tasks;
using Ng.Jobs;
using NuGet.Services.Configuration;

namespace Ng
{
    public class Program
    {
        private static ILogger _logger;

        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            if (args.Length > 0 && string.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            NgJob job = null;

            try
            {
                // Get arguments
                var arguments = CommandHelpers.GetArguments(args, 1);

                // Configure ApplicationInsights
                ApplicationInsights.Initialize(arguments.GetOrDefault<string>(Arguments.InstrumentationKey));

                // Create an ILoggerFactory
                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: true);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration, LogEventLevel.Debug);

                // Create a logger that is scoped to this class (only)
                _logger = loggerFactory.CreateLogger<Program>();

                var cancellationTokenSource = new CancellationTokenSource();
                if (args.Length == 0)
                {
                    throw new ArgumentException("Missing tool specification");
                }

                var jobName = args[0];
                LogContext.PushProperty("JobName", jobName);

                job = NgJobFactory.GetJob(jobName, loggerFactory);
                await job.Run(arguments, cancellationTokenSource.Token);
            }
            catch (ArgumentException ae)
            {
                _logger?.LogError("A required argument was not found or was malformed/invalid: {Exception}", ae);
                
                Console.WriteLine(job != null ? job.GetUsage() : NgJob.GetUsageBase());
            }
            catch (Exception e)
            {
                _logger?.LogCritical("A critical exception occured in ng.exe! {Exception}", e);
            }

            Trace.Close();
            TelemetryConfiguration.Active.TelemetryChannel.Flush();
        }
    }
}
