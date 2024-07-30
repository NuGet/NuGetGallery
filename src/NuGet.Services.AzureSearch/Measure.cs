// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch
{
    public static class Measure
    {
        public static async Task<DurationMeasurement<T>> DurationWithValueAsync<T>(Func<Task<T>> actAsync)
        {
            var stopwatch = Stopwatch.StartNew();
            var value = await actAsync();
            stopwatch.Stop();
            return new DurationMeasurement<T>(value, stopwatch.Elapsed);
        }

        public static async Task<TimeSpan> DurationAsync(Func<Task> actAsync)
        {
            var result = await DurationWithValueAsync(async () =>
            {
                await actAsync();
                return 0;
            });
            return result.Duration;
        }
    }
}