// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Gallery.Maintenance;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Tests.Gallery.Maintenance
{
    public class GalleryMaintenanceJobTests
    {
        [Fact]
        public void GetMaintenanceTasks_CreatesTasksAndDoesNotThrow()
        {
            var job = CreateJob();

            var tasks = job.GetMaintenanceTasks();

            Assert.NotEmpty(tasks);
        }

        private Job CreateJob()
        {
            var job = new Job();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<Job>();
            job.SetLogger(loggerFactory, logger);
            return job;
        }
    }
}
