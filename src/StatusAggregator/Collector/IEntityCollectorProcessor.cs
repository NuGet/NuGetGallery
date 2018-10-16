// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace StatusAggregator.Collector
{
    /// <summary>
    /// Used by <see cref="IEntityCollector"/> to fetch the latest entities from a source.
    /// </summary>
    public interface IEntityCollectorProcessor
    {
        /// <summary>
        /// A unique name for this processor.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Fetches all entities from the source newer than <paramref name="cursor"/> and returns the latest timestamp found.
        /// </summary>
        Task<DateTime?> FetchSince(DateTime cursor);
    }
}