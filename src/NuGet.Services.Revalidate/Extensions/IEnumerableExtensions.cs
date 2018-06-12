// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Revalidate
{
    public static class IEnumerableExtensions
    {
        public static List<List<T>> Batch<T>(this IEnumerable<T> collection, int batchSize)
        {
            return WeightedBatch(collection, batchSize, i => 1);
        }

        public static List<List<T>> WeightedBatch<T>(this IEnumerable<T> collection, int batchSize, Func<T, int> weightFunc)
        {
            var result = new List<List<T>>();
            var current = new List<T>();
            var currentSize = 0;

            foreach (var item in collection)
            {
                var itemWeight = weightFunc(item);

                if (currentSize != 0 && currentSize + itemWeight > batchSize)
                {
                    result.Add(current);
                    current = new List<T>();
                    currentSize = 0;
                }

                current.Add(item);
                currentSize += itemWeight;
            }

            if (current.Count > 0)
            {
                result.Add(current);
            }

            return result;
        }
    }
}
