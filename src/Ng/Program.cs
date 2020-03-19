// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Ng.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using Serilog;
using Serilog.Events;

namespace Ng
{
    public class Program
    {
        private const string HeartbeatProperty_JobLoopExitCode = "JobLoopExitCode";

        private static Microsoft.Extensions.Logging.ILogger _logger;

        public static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        public static async Task MainAsync(string[] args)
        {
            if (args.Length > 0 && string.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            NgJob job = null;
            ApplicationInsightsConfiguration applicationInsightsConfiguration = null;
            int exitCode = 0;

            try
            {
                // Get arguments
                var arguments = CommandHelpers.GetArguments(args, 1, out var secretInjector);

                // Ensure that SSLv3 is disabled and that Tls v1.2 is enabled.
                ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // Determine the job name
                if (args.Length == 0)
                {
                    throw new ArgumentException("Missing job name argument.");
                }

                var jobName = args[0];
                var instanceName = arguments.GetOrDefault(Arguments.InstanceName, jobName);
                var instrumentationKey = arguments.GetOrDefault<string>(Arguments.InstrumentationKey);
                var heartbeatIntervalSeconds = arguments.GetOrDefault<int>(Arguments.HeartbeatIntervalSeconds);

                applicationInsightsConfiguration = ConfigureApplicationInsights(
                    instrumentationKey,
                    heartbeatIntervalSeconds,
                    jobName,
                    instanceName,
                    out var telemetryClient,
                    out var telemetryGlobalDimensions);

                var loggerFactory = ConfigureLoggerFactory(applicationInsightsConfiguration);

                job = NgJobFactory.GetJob(jobName, loggerFactory, telemetryClient, telemetryGlobalDimensions);
                job.SetSecretInjector(secretInjector);

                // This tells Application Insights that, even though a heartbeat is reported, 
                // the state of the application is unhealthy when the exitcode is different from zero.
                // The heartbeat metadata is enriched with the job loop exit code.
                applicationInsightsConfiguration.DiagnosticsTelemetryModule?.AddOrSetHeartbeatProperty(
                    HeartbeatProperty_JobLoopExitCode,
                    exitCode.ToString(),
                    isHealthy: exitCode == 0);

                var cancellationTokenSource = new CancellationTokenSource();
                await job.RunAsync(arguments, cancellationTokenSource.Token);
                exitCode = 0;
            }
            catch (ArgumentException ae)
            {
                exitCode = 1;
                _logger?.LogError("A required argument was not found or was malformed/invalid: {Exception}", ae);

                Console.WriteLine(job != null ? job.GetUsage() : NgJob.GetUsageBase());
            }
            catch (KeyNotFoundException knfe)
            {
                exitCode = 1;
                _logger?.LogError("An expected key was not found. One possible cause of this is required argument has not been provided: {Exception}", knfe);

                Console.WriteLine(job != null ? job.GetUsage() : NgJob.GetUsageBase());
            }
            catch (Exception e)
            {
                exitCode = 1;
                _logger?.LogCritical("A critical exception occured in ng.exe! {Exception}", e);
            }

            applicationInsightsConfiguration.DiagnosticsTelemetryModule?.SetHeartbeatProperty(
                HeartbeatProperty_JobLoopExitCode,
                exitCode.ToString(),
                isHealthy: exitCode == 0);

            Trace.Close();
            applicationInsightsConfiguration?.TelemetryConfiguration.TelemetryChannel.Flush();
        }

        private static ILoggerFactory ConfigureLoggerFactory(ApplicationInsightsConfiguration applicationInsightsConfiguration)
        {
            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: true);
            loggerConfiguration.WriteTo.File("Log.txt", retainedFileCountLimit: 3, fileSizeLimitBytes: 1000000, rollOnFileSizeLimit: true);

            var loggerFactory = LoggingSetup.CreateLoggerFactory(
                loggerConfiguration,
                LogEventLevel.Debug,
                applicationInsightsConfiguration.TelemetryConfiguration);

            // Create a logger that is scoped to this class (only)
            _logger = loggerFactory.CreateLogger<Program>();
            return loggerFactory;
        }

        private static ApplicationInsightsConfiguration ConfigureApplicationInsights(
            string instrumentationKey,
            int heartbeatIntervalSeconds,
            string jobName,
            string instanceName,
            out ITelemetryClient telemetryClient,
            out IDictionary<string, string> telemetryGlobalDimensions)
        {
            ApplicationInsightsConfiguration applicationInsightsConfiguration;
            if (heartbeatIntervalSeconds == 0)
            {
                applicationInsightsConfiguration = ApplicationInsights.Initialize(instrumentationKey);
            }
            else
            {
                applicationInsightsConfiguration = ApplicationInsights.Initialize(
                    instrumentationKey,
                    TimeSpan.FromSeconds(heartbeatIntervalSeconds));
            }

            telemetryClient = new TelemetryClientWrapper(
                new TelemetryClient(applicationInsightsConfiguration.TelemetryConfiguration));

            telemetryGlobalDimensions = new Dictionary<string, string>();

            // Enrich telemetry with job properties and global custom dimensions                
            applicationInsightsConfiguration.TelemetryConfiguration.TelemetryInitializers.Add(
                new JobPropertiesTelemetryInitializer(jobName, instanceName, telemetryGlobalDimensions));

            return applicationInsightsConfiguration;
        }
    }
}
