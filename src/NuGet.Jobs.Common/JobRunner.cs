﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;

namespace NuGet.Jobs
{
    public static class JobRunner
    {
        public static IServiceContainer ServiceContainer;

        private static ILogger _logger;
        private static ApplicationInsightsConfiguration _applicationInsightsConfiguration;

        private const string HeartbeatProperty_JobLoopExitCode = "JobLoopExitCode";
        private const string JobSucceeded = "Job Succeeded";
        private const string JobUninitialized = "Job Failed to Initialize";
        private const string JobFailed = "Job Failed to Run";

        static JobRunner()
        {
            ServiceContainer = new ServiceContainer();
            ServiceContainer.AddService(typeof(ISecretReaderFactory), new SecretReaderFactory());
        }

        /// <summary>
        /// This is a static method to run a job whose args are passed in
        /// By default,
        ///     a) The job will be run continuously in a while loop. Could be overridden using 'once' argument
        ///     b) The sleep duration between each run when running continuously is 5000 milliSeconds. Could be overridden using '-Sleep' argument
        /// </summary>
        /// <param name="job">Job to run</param>
        /// <param name="commandLineArgs">Args contains args to the job runner like (dbg, once and so on) and for the job itself</param>
        /// <returns>The exit code, where 0 indicates success and non-zero indicates an error.</returns>
        public static async Task<int> Run(JobBase job, string[] commandLineArgs)
        {
            return await Run(job, commandLineArgs, runContinuously: null);
        }

        /// <summary>
        /// This is a static method to run a job whose args are passed in
        /// By default,
        ///     a) The job will be run the job once, irrespective of the 'once' argument.
        ///     b) The sleep duration between each run when running continuously is 5000 milliSeconds. Could be overridden using '-Sleep' argument
        /// </summary>
        /// <param name="job">Job to run</param>
        /// <param name="commandLineArgs">Args contains args to the job runner like (dbg, once and so on) and for the job itself</param>
        /// <returns>The exit code, where 0 indicates success and non-zero indicates an error.</returns>
        public static async Task<int> RunOnce(JobBase job, string[] commandLineArgs)
        {
            return await Run(job, commandLineArgs, runContinuously: false);
        }

        private static async Task<int> Run(JobBase job, string[] commandLineArgs, bool? runContinuously)
        {
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-" + JobArgumentNames.Dbg, StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            // Configure logging before Application Insights is enabled.
            // This is done so, in case Application Insights fails to initialize, we still see output.
            var loggerFactory = ConfigureLogging(job, telemetryConfiguration: null);

            int exitCode;
            try
            {
                _logger.LogInformation("Started...");

                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
                var jobArgsDictionary = JobConfigurationManager.GetJobArgsDictionary(
                    ServiceContainer,
                    loggerFactory.CreateLogger(typeof(JobConfigurationManager)),
                    commandLineArgs);

                // Setup logging
                _applicationInsightsConfiguration = ConfigureApplicationInsights(job, jobArgsDictionary);

                // Configure our logging again with Application Insights initialized.
                loggerFactory = ConfigureLogging(job, _applicationInsightsConfiguration.TelemetryConfiguration);

                var hasOnceArgument = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once);

                if (runContinuously.HasValue && hasOnceArgument)
                {
                    _logger.LogWarning(
                        $"This job is designed to {(runContinuously.Value ? "run continuously" : "run once")} so " +
                        $"the -{JobArgumentNames.Once} argument is {(runContinuously.Value ? "ignored" : "redundant")}.");
                }

                runContinuously = runContinuously ?? !hasOnceArgument;
                var reinitializeAfterSeconds = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.ReinitializeAfterSeconds);
                var sleepDuration = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.Sleep); // sleep is in milliseconds

                if (!sleepDuration.HasValue)
                {
                    sleepDuration = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.Interval);
                    if (sleepDuration.HasValue)
                    {
                        sleepDuration = sleepDuration.Value * 1000; // interval is in seconds
                    }
                }

                if (!sleepDuration.HasValue)
                {
                    if (runContinuously.Value)
                    {
                        _logger.LogInformation("SleepDuration is not provided or is not a valid integer. Unit is milliSeconds. Assuming default of 5000 ms...");
                    }

                    sleepDuration = 5000;
                }
                else if (!runContinuously.Value)
                {
                    _logger.LogWarning(
                        $"The job is designed to run once so the -{JobArgumentNames.Sleep} and " +
                        $"-{JobArgumentNames.Interval} arguments are ignored.");
                }

                if (!reinitializeAfterSeconds.HasValue)
                {
                    _logger.LogInformation(
                        $"{JobArgumentNames.ReinitializeAfterSeconds} command line argument is not provided or is not a valid integer. " +
                        "The job will reinitialize on every iteration");
                }
                else if (!runContinuously.Value)
                {
                    _logger.LogWarning(
                        $"The job is designed to run once so the -{JobArgumentNames.ReinitializeAfterSeconds} " +
                        $"argument is ignored.");
                }

                // Ensure that SSLv3 is disabled and that Tls v1.2 is enabled.
#pragma warning disable 618
                ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
#pragma warning restore 618
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // Run the job loop
                exitCode = await JobLoop(job, runContinuously.Value, sleepDuration.Value, reinitializeAfterSeconds, jobArgsDictionary);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                _logger.LogError(ex, "Job runner threw an exception: {Exception}", ex);
            }

            Trace.Close();
            _applicationInsightsConfiguration.TelemetryConfiguration.TelemetryChannel.Flush();
            _applicationInsightsConfiguration.Dispose();

            return exitCode;
        }

        private static ApplicationInsightsConfiguration ConfigureApplicationInsights(JobBase job, IDictionary<string, string> jobArgsDictionary)
        {
            ApplicationInsightsConfiguration applicationInsightsConfiguration;

            var instrumentationKey = JobConfigurationManager.TryGetArgument(
                jobArgsDictionary,
                JobArgumentNames.InstrumentationKey);

            var heartbeatIntervalSeconds = JobConfigurationManager.TryGetIntArgument(
                jobArgsDictionary,
                JobArgumentNames.HeartbeatIntervalSeconds);

            if (heartbeatIntervalSeconds.HasValue)
            {
                applicationInsightsConfiguration = ApplicationInsights.Initialize(
                    instrumentationKey,
                    TimeSpan.FromSeconds(heartbeatIntervalSeconds.Value));
            }
            else
            {
                applicationInsightsConfiguration = ApplicationInsights.Initialize(instrumentationKey);
            }

            // Determine job and instance name, for logging.
            var jobName = job.GetType().Assembly.GetName().Name;
            var instanceName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstanceName) ?? jobName;
            applicationInsightsConfiguration.TelemetryConfiguration.TelemetryInitializers.Add(
                new JobPropertiesTelemetryInitializer(jobName, instanceName, job.GlobalTelemetryDimensions));

            applicationInsightsConfiguration.TelemetryConfiguration
                .ApplicationIdProvider = new ApplicationInsightsApplicationIdProvider();

            job.SetApplicationInsightsConfiguration(applicationInsightsConfiguration);

            return applicationInsightsConfiguration;
        }

        private static ILoggerFactory ConfigureLogging(JobBase job, TelemetryConfiguration telemetryConfiguration)
        {
            var loggerFactory = LoggingSetup.CreateLoggerFactory(
                LoggingSetup.CreateDefaultLoggerConfiguration(true),
                telemetryConfiguration: telemetryConfiguration);

            var logger = loggerFactory.CreateLogger(job.GetType());

            job.SetLogger(loggerFactory, logger);
            _logger = loggerFactory.CreateLogger(typeof(JobRunner));

            return loggerFactory;
        }

        private static string PrettyPrintTime(double milliSeconds)
        {
            var seconds = (milliSeconds / 1000.0);
            var minutes = (milliSeconds / 60000.0);
            return
                $"'{milliSeconds:F3}' ms (or '{seconds:F3}' seconds or '{minutes:F3}' mins)";
        }

        private static async Task<int> JobLoop(
            JobBase job,
            bool runContinuously,
            int sleepDuration,
            int? reinitializeAfterSeconds,
            IDictionary<string, string> jobArgsDictionary)
        {
            // Run the job now
            var stopWatch = new Stopwatch();
            Stopwatch timeSinceInitialization = null;

            int exitCode = 0;

            // This tells Application Insights that, even though a heartbeat is reported, 
            // the state of the application is unhealthy when the exitcode is different from zero.
            // The heartbeat metadata is enriched with the job loop exit code.
            _applicationInsightsConfiguration.DiagnosticsTelemetryModule?.AddOrSetHeartbeatProperty(
                HeartbeatProperty_JobLoopExitCode,
                exitCode.ToString(),
                isHealthy: exitCode == 0);

            while (true)
            {
                _logger.LogInformation("Running {RunType}", (runContinuously ? " continuously..." : " once..."));
                _logger.LogInformation("SleepDuration is {SleepDuration}", PrettyPrintTime(sleepDuration));
                _logger.LogInformation("Job run started...");

                var initialized = false;
                stopWatch.Restart();

                try
                {
                    if (ShouldInitialize(reinitializeAfterSeconds, timeSinceInitialization))
                    {
                        job.Init(ServiceContainer, jobArgsDictionary);
                        timeSinceInitialization = Stopwatch.StartNew();
                    }

                    initialized = true;

                    await job.Run();

                    exitCode = 0;
                    _logger.LogInformation(JobSucceeded);
                }
                catch (Exception e)
                {
                    exitCode = 1;
                    _logger.LogError(e, "{JobState}: {Exception}", initialized ? JobFailed : JobUninitialized, e);
                }
                finally
                {
                    _logger.LogInformation("Job run ended...");
                    stopWatch.Stop();
                    _logger.LogInformation("Job run took {RunDuration}", PrettyPrintTime(stopWatch.ElapsedMilliseconds));

                    _applicationInsightsConfiguration.DiagnosticsTelemetryModule?.SetHeartbeatProperty(
                        HeartbeatProperty_JobLoopExitCode,
                        exitCode.ToString(),
                        isHealthy: exitCode == 0);
                }

                if (!runContinuously)
                {
                    // It is ok that we do not flush the logs here.
                    // Logs will be flushed at the end of Run().
                    break;
                }

                // Wait for <sleepDuration> milliSeconds and run the job again
                _logger.LogInformation("Will sleep for {SleepDuration} before the next Job run", PrettyPrintTime(sleepDuration));

                await Task.Delay(sleepDuration);
            }

            return exitCode;
        }

        private static bool ShouldInitialize(int? reinitializeAfterSeconds, Stopwatch timeSinceInitialization)
        {
            // If there is no wait time between reinitializations, always reinitialize.
            if (!reinitializeAfterSeconds.HasValue)
            {
                return true;
            }

            // A null time since last initialization indicates that the job hasn't been initialized yet.
            if (timeSinceInitialization == null)
            {
                return true;
            }

            // Otherwise, only reinitialize if the reinitialization threshold has been reached.
            return (timeSinceInitialization.Elapsed.TotalSeconds > reinitializeAfterSeconds.Value);
        }
    }
}
