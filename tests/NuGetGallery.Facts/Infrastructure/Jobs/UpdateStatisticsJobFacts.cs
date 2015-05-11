// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.Entity;
using System.Threading;
using Moq;
using NuGetGallery.Jobs;
using Xunit;

namespace NuGetGallery.Infrastructure.Jobs
{
    public class UpdateStatisticsJobFacts
    {
        public class TheExecuteMethod
        {
            [Fact]
            public void ReturnsATask()
            {
                var context = new Mock<DbContext>();
                var job = new UpdateStatisticsJob(TimeSpan.FromSeconds(10), () => context.Object, TimeSpan.MaxValue);

                var task = job.Execute();
                Assert.NotNull(task);
            }

            [Fact]
            public void CreatesNewContextOnEachRun()
            {
                int createTimes = 0;
                var job = new UpdateStatisticsJob(
                    TimeSpan.FromSeconds(10),
                    () =>
                        {
                            Interlocked.Increment(ref createTimes);
                            throw new InvalidOperationException(); // Prevent the actual calling of the query.
                        },
                    TimeSpan.MaxValue);

                RunJob(job);
                RunJob(job);

                Assert.Equal(2, createTimes);
            }

            private static void RunJob(UpdateStatisticsJob job)
            {
                var task = job.Execute();
                task.Start();
                try
                {
                    task.Wait();
                }
                catch (AggregateException)
                {
                }
            }
        }
    }
}