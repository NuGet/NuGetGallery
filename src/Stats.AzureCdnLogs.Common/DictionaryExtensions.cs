// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

// ReSharper disable once CheckNamespace
namespace System.Collections.Generic
{
    public static class DictionaryExtensions
    {
        public static List<IEnumerable<KeyValuePair<string, string>>> Partition(this IDictionary<string, string> source, int size)
        {
            // pre-order by partition key and then by descending row key
            var orderedByPartitionKey = source.OrderBy(e => e.Value).ThenByDescending(e => e.Key).ToList();

            var splitted = new List<IEnumerable<KeyValuePair<string, string>>>();

            for (var i = 0; i < orderedByPartitionKey.Count; i += size)
            {
                splitted.Add(orderedByPartitionKey.Skip(i).Take(Math.Min(size, orderedByPartitionKey.Count - i)));
            }

            return splitted;
        }
    }
}