using NuGet.Jobs.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Catalog.Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            bool runContinuously = true;
            if (args.Length > 0 && String.Equals(args[0], "dbg", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            if (args.Length > 0 && String.Equals(args[0], "once", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                runContinuously = false;
            }

            // Construct the job object. This initializes the logger(s) alone, and makes the jobName available
            var job = new Job();
            job.Logger.Log(TraceLevel.Warning, "Started...");
            job.Logger.Log(TraceLevel.Warning, "Running " + (runContinuously ? " continuously..." : " once..."));

            try
            {
                // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
                var jobArgsDictionary = JobConfigManager.GetJobArgsDictionary(job.Logger, args, job.JobName);

                // Try and get the sleep duration provided, if any. Default is 5000 milliSeconds
                string sleepDurationString;
                int sleepDuration = 5000;
                if (jobArgsDictionary.TryGetValue(JobArgumentNames.Sleep, out sleepDurationString))
                {
                    if (!Int32.TryParse(sleepDurationString, out sleepDuration))
                    {
                        job.Logger.Log(TraceLevel.Warning, "SleepDuration is not a valid integer. SleepDuration should be in milliseconds and a valid integer");
                    }
                }
                job.Logger.Log(TraceLevel.Warning, "SleepDuration is {0}", sleepDuration);

                // Initialize the job once with everything needed. Logger(s) are already initialized
                if (!job.Init(jobArgsDictionary))
                {
                    // If the job could not be initialized successfully, STOP!
                    job.Logger.Log(TraceLevel.Error, "Exiting. The job could not be initialized successfully with the arguments passed");
                    return;
                }

                // Run the job now
                do
                {
                    job.Run().Wait();
                    // Wait for <sleepDuration> milliSeconds and run the job again
                    Thread.Sleep(sleepDuration);
                } while (runContinuously);
            }
            catch (AggregateException ex)
            {
                var innerEx = ex.InnerExceptions.Count > 0 ? ex.InnerExceptions[0] : null;
                if (innerEx != null)
                {
                    job.Logger.Log(TraceLevel.Error, innerEx.ToString());
                }
                else
                {
                    job.Logger.Log(TraceLevel.Error, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                job.Logger.Log(TraceLevel.Error, ex.ToString());
            }
        }
    }
}
