// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
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

    public class DeleteExpiredApiKeysTaskTests
    {
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1000, 1, 1000)]
        [InlineData(1001, 2, 1)]
        [InlineData(2101, 3, 101)]
        public void GetCredentialKeyBatches_ReturnsExpectedBatches(
            int credentialKeyCount,
            int expectedBatchCount,
            int expectedLastBatchSize)
        {
            var credentialKeys = Enumerable.Range(1, credentialKeyCount).ToList();

            var batches = DeleteExpiredApiKeysTask.GetCredentialKeyBatches(credentialKeys).ToList();

            Assert.Equal(expectedBatchCount, batches.Count);
            Assert.All(batches, batch => Assert.InRange(batch.Count, 1, DeleteExpiredApiKeysTask.DeleteBatchSize));

            if (expectedBatchCount > 0)
            {
                Assert.Equal(expectedLastBatchSize, batches.Last().Count);
            }

            Assert.Equal(credentialKeys, batches.SelectMany(batch => batch));
        }
    }
}
