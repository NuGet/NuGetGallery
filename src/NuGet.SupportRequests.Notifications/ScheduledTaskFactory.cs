// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.SupportRequests.Notifications
{
    internal class ScheduledTaskFactory
    {
        private const string _tasksNamespace = "NuGet.SupportRequests.Notifications.Tasks";

        public static IScheduledTask Create(
            string scheduledTaskName,
            InitializationConfiguration configuration,
            Func<Task<SqlConnection>> openSupportRequestSqlConnectionAsync,
            ILoggerFactory loggerFactory)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            var scheduledTask = GetTaskOfType(
                scheduledTaskName,
                configuration,
                openSupportRequestSqlConnectionAsync,
                loggerFactory);

            return scheduledTask;
        }

        private static IScheduledTask GetTaskOfType(
            string taskName,
            InitializationConfiguration configuration,
            Func<Task<SqlConnection>> openSupportRequestSqlConnectionAsync,
            ILoggerFactory loggerFactory)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                throw new ArgumentException(nameof(taskName));
            }

            if (!taskName.EndsWith("Task", StringComparison.OrdinalIgnoreCase))
            {
                taskName = $"{taskName}Task";
            }

            var scheduledTaskType = Type.GetType($"{_tasksNamespace}.{taskName}");

            IScheduledTask scheduledTask;
            if (scheduledTaskType != null && typeof(IScheduledTask).IsAssignableFrom(scheduledTaskType))
            {
                var args = new object[] {
                    configuration,
                    openSupportRequestSqlConnectionAsync,
                    loggerFactory
                };

                scheduledTask = (IScheduledTask)Activator.CreateInstance(scheduledTaskType, args);
            }
            else
            {
                // task is invalid
                throw new NotSupportedException($"Unknown scheduled task: '{taskName}'.");
            }

            return scheduledTask;
        }
    }
}