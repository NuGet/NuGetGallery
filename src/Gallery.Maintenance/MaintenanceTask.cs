// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gallery.Maintenance
{
    /// <summary>
    /// A task to be run as a part of Gallery maintenance. Makes SQL queries against the Gallery database.
    /// </summary>
    /// <remarks>
    /// Each <see cref="MaintenanceTask"/> must have a constructor that takes in an <see cref="ILogger{TCategoryName}"/> where the category name is the name of the task.
    /// </remarks>
    public abstract class MaintenanceTask
    {
        protected ILogger<MaintenanceTask> _logger;

        /// <summary>
        /// Run the maintenance task for the Gallery.
        /// </summary>
        /// <param name="job">Gallery maintenance job, for SQL connection and logging.</param>
        public abstract Task RunAsync(Job job);

        public MaintenanceTask(ILogger<MaintenanceTask> logger)
        {
            _logger = logger;
        }
    }
}
