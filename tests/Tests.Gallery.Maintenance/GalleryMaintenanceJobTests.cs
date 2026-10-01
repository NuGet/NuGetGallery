// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
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

        [Fact]
        public async Task Main_ReturnsFailureWhenConfigurationIsMissing()
        {
            var path = Path.Combine(Path.GetTempPath(), $"gallery-maintenance-{Guid.NewGuid():N}.json");

            var exitCode = await Program.Main(new[] { "-Configuration", path, "-Once" });

            Assert.NotEqual(0, exitCode);
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
