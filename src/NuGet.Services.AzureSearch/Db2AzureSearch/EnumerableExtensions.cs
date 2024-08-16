// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<List<T>> Batch<T>(
            this IEnumerable<T> sequence,
            Func<T, int> getItemSize,
            int desiredSize)
        {
            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            if (getItemSize == null)
            {
                throw new ArgumentNullException(nameof(getItemSize));
            }

            if (desiredSize < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(desiredSize),
                    "The max size must be greater than or equal to zero.");
            }

            var list = new List<T>();
            var sizeSoFar = 0;

            foreach (var item in sequence)
            {
                var itemSize = getItemSize(item);

                if (sizeSoFar + itemSize > desiredSize)
                {
                    if (list.Count > 0)
                    {
                        yield return list;
                    }

                    list = new List<T> { item };
                    sizeSoFar = 0;
                }
                else
                {
                    list.Add(item);
                }

                sizeSoFar += itemSize;
            }

            if (list.Count > 0)
            {
                yield return list;
            }
        }
    }
}
