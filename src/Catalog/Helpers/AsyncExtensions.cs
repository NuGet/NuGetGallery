// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class AsyncExtensions
    {
        public static Task ForEachAsync<T>(this IEnumerable<T> enumerable, int maxDegreeOfParallelism, Func<T, Task> func)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDegreeOfParallelism),
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            return Task.WhenAll(
                from partition in Partitioner.Create(enumerable).GetPartitions(maxDegreeOfParallelism)
                select Task.Run(async delegate
                {
                    using (partition)
                    {
                        while (partition.MoveNext())
                        {
                            await func(partition.Current);
                        }
                    }
                }));
        }
    }
}