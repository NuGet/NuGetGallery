// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// ReSharper disable once CheckNamespace
namespace System.Collections.Generic
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> source, int size)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    yield return YieldPartitionElements(enumerator, size - 1);
                }
            }
        }

        private static IEnumerable<T> YieldPartitionElements<T>(IEnumerator<T> source, int size)
        {
            yield return source.Current;
            for (var i = 0; i < size && source.MoveNext(); i++)
            {
                yield return source.Current;
            }
        }
    }
}