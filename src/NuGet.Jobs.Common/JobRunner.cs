// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public static class JobRunner
    {
        private const string JobSucceeded = "Job Succeeded";
        private const string JobFailed = "Job Failed";
        private const string JobCrashed = "Job Crashed";
        private static string PrettyPrintTime(double milliSeconds)
        {
            const string PrecisionSpecifier = "F3";
            return String.Format("'{0}' ms (or '{1}' seconds or '{2}' mins)",
                milliSeconds.ToString(PrecisionSpecifier),  // Time in milliSeconds
                (milliSeconds / 1000.0).ToString(PrecisionSpecifier),  // Time in seconds
                (milliSeconds / 60000.0).ToString(PrecisionSpecifier));  // Time in minutes
        }
        private static void SetJobTraceListener(JobBase job, bool consoleLogOnly)
        {
            if(consoleLogOnly)
            {
                job.SetJobTraceListener(new JobTraceListener(job.JobName));
                Trace.TraceWarning("You have chosen not to log messages to Azure blob storage. Note that this is NOT recommended");
            }
            else
            {
                job.SetJobTraceListener(new AzureBlobJobTraceListener(job.JobName));
            }
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
            string jobEndMessage = null;
            if (commandLineArgs.Length > 0 && String.Equals(commandLineArgs[0], "-dbg", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            bool consoleLogOnly = false;
            if (commandLineArgs.Length > 0 && String.Equals(commandLineArgs[0], "-ConsoleLogOnly", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                consoleLogOnly = true;
            }

            // Set JobTraceListener. This will be done on every job run as well
            SetJobTraceListener(job, consoleLogOnly);
            try
            {
                Trace.TraceInformation("Started...");

                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
                var jobArgsDictionary = JobConfigManager.GetJobArgsDictionary(job.JobTraceListener, commandLineArgs, job.JobName);

                bool runContinuously = !JobConfigManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once);
                int? sleepDuration = JobConfigManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.Sleep);

                // Setup the job for running
                JobSetup(job, jobArgsDictionary, ref sleepDuration);

                // Run the job loop
                jobEndMessage = await JobLoop(job, runContinuously, sleepDuration.Value, consoleLogOnly);
            }
            catch (AggregateException ex)
            {
                var innerEx = ex.InnerExceptions.Count > 0 ? ex.InnerExceptions[0] : null;
                if (innerEx != null)
                {
                    Trace.TraceError("[FAILED]: " + innerEx.ToString());
                }
                else
                {
                    Trace.TraceError("[FAILED]: " + ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FAILED]: " + ex.ToString());
            }

            // Call FlushAll here. This is VERY IMPORTANT
            // Exception message(s) when the job faults are still in the queue. Need to be flushed
            // Also, when the job is only run once, FlushAll is important again
            job.JobTraceListener.Close();
        }

        private static void JobSetup(JobBase job, IDictionary<string, string> jobArgsDictionary, ref int? sleepDuration)
        {
            if (JobConfigManager.TryGetBoolArgument(jobArgsDictionary, "dbg"))
            {
                throw new ArgumentException("-dbg is a special argument and should be the first argument...");
            }

            if (JobConfigManager.TryGetBoolArgument(jobArgsDictionary, "ConsoleLogOnly"))
            {
                throw new ArgumentException("-ConsoleLogOnly is a special argument and should be the first argument (can be the second if '-dbg' is used)...");
            }

            if (sleepDuration == null)
            {
                Trace.TraceWarning("SleepDuration is not provided or is not a valid integer. Unit is milliSeconds. Assuming default of 5000 ms...");
                sleepDuration = 5000;
            }

            // Initialize the job once with everything needed. JobTraceListener(s) are already initialized
            if (!job.Init(jobArgsDictionary))
            {
                // If the job could not be initialized successfully, STOP!
                Trace.TraceError("Exiting. The job could not be initialized successfully with the arguments passed");
                return;
            }
        }

        private static async Task<string> JobLoop(JobBase job, bool runContinuously, int sleepDuration, bool consoleLogOnly)
        {
            // Run the job now
            Stopwatch stopWatch = new Stopwatch();
            bool success;
            do
            {
                Trace.WriteLine("Running " + (runContinuously ? " continuously..." : " once..."));
                Trace.WriteLine("SleepDuration is " + PrettyPrintTime(sleepDuration));
                Trace.WriteLine("Job run started...");

                // Force a flush here to create a blob corresponding to run indicating that the run has started
                job.JobTraceListener.Flush();

                stopWatch.Restart();
                success = await job.Run();
                stopWatch.Stop();

                Trace.WriteLine("Job run ended...");
                Trace.TraceInformation("Job run took {0}", PrettyPrintTime(stopWatch.ElapsedMilliseconds));
                if(success)
                {
                    Trace.TraceInformation(JobSucceeded);
                }
                else
                {
                    Trace.TraceWarning(JobFailed);
                }

                // At this point, FlushAll is not called, So, what happens when the job is run only once?
                // Since, FlushAll is called right at the end of the program, this is no issue
                if (!runContinuously) break;

                // Wait for <sleepDuration> milliSeconds and run the job again
                Trace.WriteLine(String.Format("Will sleep for {0} before the next Job run", PrettyPrintTime(sleepDuration)));

                // Flush All the logs for this run
                job.JobTraceListener.Close();

                // Use Console.WriteLine when you don't want it to be logged in Azure blobs
                Console.WriteLine("Sleeping for {0} before the next job run", PrettyPrintTime(sleepDuration));
                Thread.Sleep(sleepDuration);

                SetJobTraceListener(job, consoleLogOnly);
            } while (true);

            return success ? JobSucceeded : JobFailed;
        }
    }
}
