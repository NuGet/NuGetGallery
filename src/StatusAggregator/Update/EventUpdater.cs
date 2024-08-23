// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Update
{
    public class EventUpdater : IComponentAffectingEntityUpdater<EventEntity>
    {
        private readonly AggregationEntityUpdater<IncidentGroupEntity, EventEntity> _aggregationUpdater;
        private readonly EventMessagingUpdater _messagingUpdater;

        public EventUpdater(
            AggregationEntityUpdater<IncidentGroupEntity, EventEntity> aggregationUpdater,
            EventMessagingUpdater messagingUpdater)
        {
            _aggregationUpdater = aggregationUpdater ?? throw new ArgumentNullException(nameof(aggregationUpdater));
            _messagingUpdater = messagingUpdater ?? throw new ArgumentNullException(nameof(messagingUpdater));
        }

        public async Task UpdateAsync(EventEntity entity, DateTime cursor)
        {
            await _aggregationUpdater.UpdateAsync(entity, cursor);
            await _messagingUpdater.UpdateAsync(entity, cursor);
        }
    }
}
