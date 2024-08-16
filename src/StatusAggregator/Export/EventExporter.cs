// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Export
{
    public class EventExporter : IEventExporter
    {
        private readonly ITableWrapper _table;
        private readonly ILogger<EventExporter> _logger;

        public EventExporter(
            ITableWrapper table,
            ILogger<EventExporter> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Event Export(EventEntity eventEntity)
        {
            _logger.LogInformation("Exporting event {EventRowKey}.", eventEntity.RowKey);

            var messages = _table.GetChildEntities<MessageEntity, EventEntity>(eventEntity)
                .ToList()
                // Don't show empty messages.
                .Where(m => !string.IsNullOrEmpty(m.Contents))
                .ToList();

            _logger.LogInformation("Event has {MessageCount} messages that are not empty.", messages.Count);

            if (!messages.Any())
            {
                return null;
            }

            return new Event(
                eventEntity.AffectedComponentPath,
                eventEntity.StartTime,
                eventEntity.EndTime,
                messages
                    .OrderBy(m => m.Time)
                    .Select(m => new Message(m.Time, m.Contents)));
        }
    }
}