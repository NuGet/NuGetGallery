// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;

namespace StatusAggregator.Update
{
    public class EventMessagingUpdater : IComponentAffectingEntityUpdater<EventEntity>
    {
        private readonly IMessageChangeEventProvider _provider;
        private readonly IMessageChangeEventIterator _iterator;

        private readonly ILogger<EventMessagingUpdater> _logger;

        public EventMessagingUpdater(
            IMessageChangeEventProvider provider,
            IMessageChangeEventIterator iterator,
            ILogger<EventMessagingUpdater> logger)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _iterator = iterator ?? throw new ArgumentNullException(nameof(iterator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task UpdateAsync(EventEntity eventEntity, DateTime cursor)
        {
            _logger.LogInformation("Updating messages for event {EventRowKey} at {Cursor}.", eventEntity.RowKey, cursor);
            var changes = _provider.Get(eventEntity, cursor);
            return _iterator.IterateAsync(changes, eventEntity);
        }
    }
}
