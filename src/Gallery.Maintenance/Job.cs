// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Logging;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Runs all <see cref="IMaintenanceTask"/>s against the Gallery database.
    /// </summary>
    public class Job : JobBase
    {
        private static readonly Lazy<IEnumerable<IMaintenanceTask>> _tasks = new Lazy<IEnumerable<IMaintenanceTask>>(GetMaintenanceTasks);

        public SqlConnectionStringBuilder GalleryDatabase { get; private set; }

        public ILogger Logger { get; private set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(ConsoleLogOnly);
                var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
                Logger = loggerFactory.CreateLogger<Job>();

                var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryDatabase);
                GalleryDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
            }
            catch (Exception exception)
            {
                Logger.LogCritical(LogEvents.JobInitFailed, exception, "Failed to initialize job!");

                return false;
            }

            return true;
        }

        public override async Task<bool> Run()
        {
            var result = true;

            foreach (var task in _tasks.Value)
            {
                var taskName = task.GetType().Name;

                try
                {
                    Logger.LogInformation("Running task '{taskName}'...", taskName);

                    if (await task.RunAsync(this))
                    {
                        Logger.LogInformation("Finished task '{taskName}'.", taskName);
                    }
                    else
                    {
                        Logger.LogWarning("Task '{taskName}' returned failure status.", taskName);
                        result = false;
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogCritical(LogEvents.JobRunFailed, exception, "Job run failed for task '{taskName}'.", taskName);
                    result = false;
                }
            }

            return result;
        }

        private static IEnumerable<IMaintenanceTask> GetMaintenanceTasks()
        {
            var taskBaseType = typeof(IMaintenanceTask);

            return taskBaseType.Assembly.GetTypes()
                .Where(type => type.IsClass && taskBaseType.IsAssignableFrom(type))
                .Select(type => (IMaintenanceTask)Activator.CreateInstance(type));
        }
    }
}
