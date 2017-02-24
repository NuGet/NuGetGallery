// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Logging;

namespace NuGet.SupportRequests.Notifications
{
    internal class Job
        : JobBase
    {
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private IDictionary<string, string> _jobArgsDictionary;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                _loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                _logger = _loggerFactory.CreateLogger<Job>();

                if (!jobArgsDictionary.ContainsKey(JobArgumentNames.ScheduledTask))
                {
                    throw new NotSupportedException("The required argument -Task is missing.");
                }

                _jobArgsDictionary = jobArgsDictionary;
            }
            catch (Exception exception)
            {
                _logger.LogCritical(LogEvents.JobInitFailed, exception, "Failed to initialize job!");

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            try
            {
                var scheduledTask = ScheduledTaskFactory.Create(_jobArgsDictionary, _loggerFactory);

                await scheduledTask.RunAsync();
            }
            catch (Exception exception)
            {
                _logger.LogCritical(LogEvents.JobRunFailed, exception, "Job run failed!");
                return false;
            }

            return true;
        }
    }
}