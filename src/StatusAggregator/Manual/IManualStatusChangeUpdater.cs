// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    /// <summary>
    /// Monitors a storage for the manual status changes posted to it.
    /// </summary>
    public interface IManualStatusChangeUpdater
    {
        /// <summary>
        /// An identifier for what storage that the manual status changes are being monitored from.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Processes all manual status changes in the storage that are more recent than <paramref name="cursor"/>.
        /// </summary>
        Task<DateTime?> ProcessNewManualChanges(DateTime cursor);
    }
}
