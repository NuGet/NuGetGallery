// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.AzureJobTraceListener
{
    internal class Job : JobBase
    {
        private static int BaseNumber = 2;

        private const string ScenarioArgumentName = "scenario";
        private const string LogCountArgumentName = "logcount";
        private const string HelpMessage = @"Please provide
                        1 for successful job,
                        2 for failed job,
                        3 for crashed job,
                        4 for successful job with heavy logging,
                        5 for job with multiple threads,
                        6 for job that calls Trace.Close from multiple threads,
                        7 for job that calls Job.JobTraceListener.Close from multiple threads";

        private int? JobScenario { get; set; }

        private int? LogCount { get; set; }
        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            JobScenario = JobConfigManager.TryGetIntArgument(jobArgsDictionary, ScenarioArgumentName);
            if(JobScenario == null)
            {
                throw new ArgumentException("Argument '"+ ScenarioArgumentName +"' is mandatory." + HelpMessage);
            }

            LogCount = JobConfigManager.TryGetIntArgument(jobArgsDictionary, LogCountArgumentName);

            return true;
        }

        public async override Task<bool> Run()
        {
            LogCount = LogCount ?? AzureBlobJobTraceListener.MaxLogBatchSize * 2;
            switch(JobScenario)
            {
                case 1:
                    return true;
                    
                case 2:
                    return false;

                case 3:
                    throw new Exception("Job crashed test");

                case 4:
                    for(int i = 0; i < LogCount; i++)
                    {
                        Trace.WriteLine("Message number : " + i);
                    }
                    return true;

                case 5:
	                Trace.TraceInformation("Started");
                    LogALotSetup(3);
	                Trace.TraceInformation("Ended");
                    return true;

                case 6:
                    // Imitates the scenario where Trace.Close (consequently, AzureBlobJobTraceListener.Close is called from multiple threads)
                    // NOTE that the first call to Trace.Close will likely have removed AzureBlobJobTraceListener from the Trace.Listeners list,
                    // hence, subsequent calls will not really call the AzureBlobJobTraceListener.close
                    Trace.TraceInformation("Started");
                    LogALotSetup(3);
                    Trace.TraceInformation("Ended");
                    Task[] traceCloseTasks = new Task[6];
                    for (int i = 0; i < 6; i++)
                    {
                        traceCloseTasks[i] = TraceClose();
                    }
                    Task.WaitAll(traceCloseTasks);
                    return true;

                case 7:
                    // Imitates the scenario where JobBase.JobTraceListener.Close is called directly from multiple threads,
                    // which is basically like calling AzureBlobJobTraceListener.Close from multiple threads)
                    Trace.TraceInformation("Started");
                    LogALotSetup(3);
                    Trace.TraceInformation("Ended");
                    Task[] jobTraceListenerCloseTasks = new Task[6];
                    for (int i = 0; i < 6; i++)
                    {
                        jobTraceListenerCloseTasks[i] = JobTraceListenerClose();
                    }
                    Task.WaitAll(jobTraceListenerCloseTasks);
                    return true;

                default:
                    throw new ArgumentException("Unknown scenario. " + HelpMessage);
            }
        }

        private void LogALotSetup(int numberOfThreads = 3)
        {
            Task[] tasks = new Task[numberOfThreads];
            for (int i = 0; i < numberOfThreads; i++)
            {
                tasks[i] = LogALot();
            }
            Task.WaitAll(tasks);
        }

        private async Task LogALot()
        {
            int baseNumber = Interlocked.Increment(ref BaseNumber);
            for (int i = 1; i <= 10; i++)
            {
                Trace.TraceInformation((i * baseNumber).ToString());
                // Following sleep is simple emulation of some work taking place
                Thread.Sleep(100);
            }
        }

        private async Task TraceClose()
        {
            Trace.Close();
        }

        private async Task JobTraceListenerClose()
        {
            JobTraceListener.Close();
        }
    }
}
