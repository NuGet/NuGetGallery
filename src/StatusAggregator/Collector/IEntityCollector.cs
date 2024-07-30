// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace StatusAggregator.Collector
{
    /// <summary>
    /// Collects new entities from a source.
    /// </summary>
    public interface IEntityCollector
    {
        /// <summary>
        /// A unique name for this collector.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Fetches all new entities from the source and returns the newest timestamp found.
        /// </summary>
        Task<DateTime> FetchLatest();
    }
}