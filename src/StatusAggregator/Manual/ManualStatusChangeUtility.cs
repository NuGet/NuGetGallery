// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using System;

namespace StatusAggregator.Manual
{
    public static class ManualStatusChangeUtility
    {
        /// <summary>
        /// Compares <paramref name="eventEntity"/> to <paramref name="eventIsActive"/> and updates <paramref name="eventEntity"/>'s <see cref="EventEntity.EndTime"/> if it should be updated.
        /// </summary>
        /// <param name="timestamp">A <see cref="DateTime"/> that represents when <paramref name="eventEntity"/> was closed if <paramref name="eventIsActive"/> is <c>false</c>.</param>
        /// <returns>
        /// Whether or not <paramref name="eventEntity"/> was updated.
        /// If true, the changes to <paramref name="eventEntity"/> should be saved to the table.
        /// </returns>
        /// <remarks>
        /// An event cannot be reactivated, so <paramref name="eventEntity"/> WILL NOT be updated if it is already deactivated.
        /// </remarks>
        public static bool UpdateEventIsActive(EventEntity eventEntity, bool eventIsActive, DateTime timestamp)
        {
            var shouldUpdateEvent = ShouldEventBeActive(eventEntity, eventIsActive, timestamp);
            if (shouldUpdateEvent)
            {
                eventEntity.EndTime = timestamp;
            }

            return shouldUpdateEvent;
        }

        /// <summary>
        /// Compares <paramref name="eventEntity"/> to <paramref name="eventIsActive"/> and returns whether or not <paramref name="eventEntity"/>'s <see cref="EventEntity.EndTime"/> should be updated.
        /// </summary>
        /// <param name="timestamp">A <see cref="DateTime"/> that represents when <paramref name="eventEntity"/> was closed if <paramref name="eventIsActive"/> is <c>false</c>.</param>
        /// <returns>
        /// Whether or not <paramref name="eventEntity"/>'s <see cref="EventEntity.EndTime"/> should be updated.
        /// </returns>
        /// <remarks>
        /// An event cannot be reactivated, so <paramref name="eventEntity"/> SHOULD NOT be updated if it is already deactivated.
        /// </remarks>
        public static bool ShouldEventBeActive(EventEntity eventEntity, bool eventIsActive, DateTime timestamp)
        {
            eventEntity = eventEntity ?? throw new ArgumentNullException(nameof(eventEntity));

            return !eventIsActive && eventEntity.EndTime == null;
        }
    }
}
