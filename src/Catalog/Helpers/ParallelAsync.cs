// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class ParallelAsync
    {
        /// <summary>
        /// Creates a number of tasks specified by <paramref name="degreeOfParallelism"/> using <paramref name="taskFactory"/> and then runs them in parallel.
        /// </summary>
        /// <param name="taskFactory">Creates each task to run.</param>
        /// <param name="degreeOfParallelism">The number of tasks to create. Defaults to <see cref="ServicePointManager.DefaultConnectionLimit"/></param>
        /// <returns>A task that completes when all tasks have completed.</returns>
        public static Task Repeat(Func<Task> taskFactory, int? degreeOfParallelism = null)
        {
            return Task.WhenAll(
                Enumerable
                    .Repeat(taskFactory, degreeOfParallelism ?? ServicePointManager.DefaultConnectionLimit)
                    .Select(f => f()));
        }
    }
}