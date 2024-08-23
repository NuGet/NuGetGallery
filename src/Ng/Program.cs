﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ng.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
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

                InitializeServiceProvider(
                    arguments,
                    secretInjector,
                    applicationInsightsConfiguration,
                    telemetryClient,
                    loggerFactory);

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
                _logger?.LogError(ae, "A required argument was not found or was malformed/invalid: {Exception}", ae);

                Console.WriteLine(job != null ? job.GetUsage() : NgJob.GetUsageBase());
            }
            catch (KeyNotFoundException knfe)
            {
                exitCode = 1;
                _logger?.LogError(knfe, "An expected key was not found. One possible cause of this is required argument has not been provided: {Exception}", knfe);

                Console.WriteLine(job != null ? job.GetUsage() : NgJob.GetUsageBase());
            }
            catch (Exception e)
            {
                exitCode = 1;
                _logger?.LogCritical(e, "A critical exception occured in ng.exe! {Exception}", e);
            }

            applicationInsightsConfiguration?.DiagnosticsTelemetryModule?.SetHeartbeatProperty(
                HeartbeatProperty_JobLoopExitCode,
                exitCode.ToString(),
                isHealthy: exitCode == 0);

            Trace.Close();
            applicationInsightsConfiguration?.TelemetryConfiguration.TelemetryChannel.Flush();
        }

        /// <summary>
        /// This mimics the approach taking in NuGet.Job's JsonConfigurationJob. We add common infrastructure to the
        /// dependency injection container and load Autofac modules in the current assembly. This gives us a hook to
        /// customize the initialization of this program. Today this service provider is only used for modifying
        /// telemetry configuration but it could eventually be used in the <see cref="NgJobFactory"/>.
        /// </summary>
        private static IServiceProvider InitializeServiceProvider(
            IDictionary<string, string> arguments,
            ISecretInjector secretInjector,
            ApplicationInsightsConfiguration applicationInsightsConfiguration,
            ITelemetryClient telemetryClient,
            ILoggerFactory loggerFactory)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(arguments);

            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(secretInjector);
            services.AddSingleton(applicationInsightsConfiguration.TelemetryConfiguration);
            services.AddSingleton<IConfiguration>(configurationBuilder.Build());

            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(loggerFactory);
            services.AddLogging();

            services.AddSingleton(telemetryClient);

            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);
            containerBuilder.RegisterAssemblyModules(typeof(Program).Assembly);

            return new AutofacServiceProvider(containerBuilder.Build());
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
