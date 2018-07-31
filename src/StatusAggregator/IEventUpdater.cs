// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    /// <summary>
    /// Handles updating any active <see cref="EventEntity"/>s.
    /// </summary>
    public interface IEventUpdater
    {
        /// <summary>
        /// Updates all active <see cref="EventEntity"/>s.
        /// </summary>
        /// <param name="cursor">The current timestamp processed by the job.</param>
        Task UpdateActiveEvents(DateTime cursor);

        /// <summary>
        /// Update <paramref name="eventEntity"/> given <paramref name="cursor"/>.
        /// Determines whether or not to deactivate <paramref name="eventEntity"/> and updates any messages associated with the event.
        /// </summary>
        /// <param name="cursor">The current timestamp processed by the job.</param>
        /// <returns>Whether or not <paramref name="eventEntity"/> was deactivated.</returns>
        Task<bool> UpdateEvent(EventEntity eventEntity, DateTime cursor);
    }
}
