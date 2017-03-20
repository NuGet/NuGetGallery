// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Gallery.Maintenance
{
    /// <summary>
    /// A task to be run as a part of Gallery maintenance. Makes SQL queries against the Gallery database.
    /// </summary>
    public interface IMaintenanceTask
    {
        /// <summary>
        /// Run the maintenance task for the Gallery.
        /// </summary>
        /// <param name="job">Gallery maintenance job, for SQL connection and logging.</param>
        /// <returns>True for success, false for exception or other failure.</returns>
        Task<bool> RunAsync(Job job);
    }
}
