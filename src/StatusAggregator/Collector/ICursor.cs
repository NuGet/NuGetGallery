// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace StatusAggregator.Collector
{
    /// <summary>
    /// Persists a <see cref="DateTime"/> that represents that job's current progress by a string.
    /// </summary>
    public interface ICursor
    {
        /// <summary>
        /// Gets the current progress of the job by <paramref name="name"/>.
        /// </summary>
        Task<DateTime> Get(string name);

        /// <summary>
        /// Saves <paramref name="value"/> as the current progress of the job by <paramref name="name"/>.
        /// </summary>
        Task Set(string name, DateTime value);
    }
}