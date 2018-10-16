// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;

namespace StatusAggregator.Table
{
    public static class TablePartitionKeys
    {
        /// <summary>
        /// Gets the partition key associated with <typeparamref name="T"/>.
        /// Each <typeparamref name="T"/> is mapped to a single key.
        /// </summary>
        public static string Get<T>()
        {
            var match = PartitionKeyMap.FirstOrDefault(m => m.Matches(typeof(T)));
            if (match != null)
            {
                return match.PartitionKey;
            }

            throw new ArgumentException("There is no mapping of the specified type to a partition key!", nameof(T));
        }

        /// <remarks>
        /// This was not implemented as a dictionary because it is not possible to construct a <see cref="IEqualityComparer{T}.GetHashCode(T)"/> that works with type inheritance.
        /// 
        /// Proof:
        /// B and C are subclasses of A, so B and C must have the same hashcode as A.
        /// However, B must not have the same hashcode as C because B is not C and C is not B.
        /// Therefore, B and C must have a hashcode that is both identical AND different.
        /// This is not possible.
        /// </remarks>
        private static readonly IEnumerable<PartitionKeyMapping> PartitionKeyMap = new[]
        {
            new PartitionKeyMapping(typeof(CursorEntity), CursorEntity.DefaultPartitionKey),
            new PartitionKeyMapping(typeof(IncidentEntity), IncidentEntity.DefaultPartitionKey),
            new PartitionKeyMapping(typeof(IncidentGroupEntity), IncidentGroupEntity.DefaultPartitionKey),
            new PartitionKeyMapping(typeof(EventEntity), EventEntity.DefaultPartitionKey),
            new PartitionKeyMapping(typeof(MessageEntity), MessageEntity.DefaultPartitionKey),
            new PartitionKeyMapping(typeof(ManualStatusChangeEntity), ManualStatusChangeEntity.DefaultPartitionKey),
        };

        private class PartitionKeyMapping
        {
            public Type Type { get; }
            public string PartitionKey { get; }

            public PartitionKeyMapping(Type type, string partitionKey)
            {
                Type = type;
                PartitionKey = partitionKey;
            }

            public bool Matches(Type type)
            {
                return Type.IsAssignableFrom(type);
            }
        }
    }
}
