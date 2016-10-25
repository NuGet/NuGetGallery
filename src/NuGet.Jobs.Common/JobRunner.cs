// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Configuration;

namespace NuGet.Jobs
{
    public static class JobRunner
    {
        public static IServiceContainer ServiceContainer;

        private const string JobUninitialized = "Job Failed to Initialize";
        private const string JobSucceeded = "Job Succeeded";
        private const string JobFailed = "Job Failed";

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
        /// <returns></returns>
        public static async Task Run(JobBase job, string[] commandLineArgs)
        {
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-dbg", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            bool consoleLogOnly = false;
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-ConsoleLogOnly", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                consoleLogOnly = true;
            }

            // Set the default trace listener, so if we get args parsing issues they will be printed. This will be overriden by the configured trace listener
            // after config is parsed.
            job.SetJobTraceListener(new JobTraceListener());

            try
            {
                Trace.TraceInformation("Started...");

                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
                var jobArgsDictionary = JobConfigurationManager.GetJobArgsDictionary(commandLineArgs, job.JobName, (ISecretReaderFactory)ServiceContainer.GetService(typeof(ISecretReaderFactory)));

                // Set JobTraceListener. This will be done on every job run as well
                SetJobTraceListener(job, consoleLogOnly, jobArgsDictionary);

                var runContinuously = !jobArgsDictionary.GetOrNull<bool>(JobArgumentNames.Once) ?? true;
                var sleepDuration = jobArgsDictionary.GetOrNull<int>(JobArgumentNames.Sleep); // sleep is in milliseconds
                if (!sleepDuration.HasValue)
                {
                    sleepDuration = jobArgsDictionary.GetOrNull<int>(JobArgumentNames.Interval);
                    if (sleepDuration.HasValue)
                    {
                        sleepDuration = sleepDuration.Value * 1000; // interval is in seconds
                    }
                }

                // Setup the job for running
                JobSetup(job, consoleLogOnly, jobArgsDictionary, ref sleepDuration);

                // Run the job loop
                await JobLoop(job, runContinuously, sleepDuration.Value, consoleLogOnly, jobArgsDictionary);
            }
            catch (AggregateException ex)
            {
                var innerEx = ex.InnerExceptions.Count > 0 ? ex.InnerExceptions[0] : null;
                if (innerEx != null)
                {
                    Trace.TraceError("[FAILED]: " + innerEx);
                }
                else
                {
                    Trace.TraceError("[FAILED]: " + ex);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FAILED]: " + ex);
            }

            // Flush here. This is VERY IMPORTANT!
            // Exception messages from when the job faults are still in the queue and need to be flushed.
            // Also, if the job is only run once, this is what flushes the queue.
            job.JobTraceListener.Close();
        }

        private static string PrettyPrintTime(double milliSeconds)
        {
            var seconds = (milliSeconds/1000.0);
            var minutes = (milliSeconds/60000.0);
            return
                $"'{milliSeconds:F3}' ms (or '{seconds:F3}' seconds or '{minutes:F3}' mins)";
        }

        private static void SetJobTraceListener(JobBase job, bool consoleLogOnly, IDictionary<string, string> argsDictionary)
        {
            if (consoleLogOnly)
            {
                job.SetJobTraceListener(new JobTraceListener());
            }
            else
            {
                var connectionString = argsDictionary[JobArgumentNames.LogsAzureStorageConnectionString];
                job.SetJobTraceListener(new AzureBlobJobTraceListener(job.JobName, connectionString));
            }
        }

        private static void JobSetup(JobBase job, bool consoleLogOnly, IDictionary<string, string> jobArgsDictionary, ref int? sleepDuration)
        {
            if (jobArgsDictionary.GetOrNull<bool>("dbg") ?? false)
            {
                throw new ArgumentException("-dbg is a special argument and should be the first argument...");
            }

            if (jobArgsDictionary.GetOrNull<bool>("ConsoleLogOnly") ?? false)
            {
                throw new ArgumentException("-ConsoleLogOnly is a special argument and should be the first argument (can be the second if '-dbg' is used)...");
            }

            if (sleepDuration == null)
            {
                Trace.TraceWarning("SleepDuration is not provided or is not a valid integer. Unit is milliSeconds. Assuming default of 5000 ms...");
                sleepDuration = 5000;
            }

            job.ConsoleLogOnly = consoleLogOnly;
        }

        private static async Task JobLoop(JobBase job, bool runContinuously, int sleepDuration, bool consoleLogOnly, IDictionary<string, string> jobArgsDictionary)
        {
            // Run the job now
            var stopWatch = new Stopwatch();

            while (true)
            {
                Trace.WriteLine("Running " + (runContinuously ? " continuously..." : " once..."));
                Trace.WriteLine("SleepDuration is " + PrettyPrintTime(sleepDuration));
                Trace.TraceInformation("Job run started...");

                // Force a flush here to create a blob corresponding to run indicating that the run has started
                job.JobTraceListener.Flush();

                stopWatch.Restart();
                var initialized = job.Init(jobArgsDictionary);
                var succeeded = initialized && await job.Run();
                stopWatch.Stop();

                Trace.WriteLine("Job run ended...");
                if (initialized)
                {
                    Trace.TraceInformation("Job run took {0}", PrettyPrintTime(stopWatch.ElapsedMilliseconds));

                    if (succeeded)
                    {
                        Trace.TraceInformation(JobSucceeded);
                    }
                    else
                    {
                        Trace.TraceWarning(JobFailed);
                    }
                }
                else
                {
                    Trace.TraceWarning(JobUninitialized);
                }
                
                if (!runContinuously)
                {
                    // It is ok that we do not flush the logs here.
                    // Logs will be flushed at the end of Run().
                    break;
                }

                // Wait for <sleepDuration> milliSeconds and run the job again
                Trace.TraceInformation("Will sleep for {0} before the next Job run", PrettyPrintTime(sleepDuration));

                // Flush all the logs for this run
                job.JobTraceListener.Close();
                Thread.Sleep(sleepDuration);

                SetJobTraceListener(job, consoleLogOnly, jobArgsDictionary);
            }
        }
    }
}
