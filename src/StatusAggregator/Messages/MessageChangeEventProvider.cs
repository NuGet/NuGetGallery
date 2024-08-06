// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Messages
{
    public class MessageChangeEventProvider : IMessageChangeEventProvider
    {
        private readonly ITableWrapper _table;
        private readonly IIncidentGroupMessageFilter _filter;

        private readonly ILogger<MessageChangeEventProvider> _logger;

        public MessageChangeEventProvider(
            ITableWrapper table,
            IIncidentGroupMessageFilter filter,
            ILogger<MessageChangeEventProvider> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<MessageChangeEvent> Get(EventEntity eventEntity, DateTime cursor)
        {
            var linkedGroups = _table.GetChildEntities<IncidentGroupEntity, EventEntity>(eventEntity).ToList();
            var events = new List<MessageChangeEvent>();
            _logger.LogInformation("Event has {IncidentGroupsCount} linked incident groups.", linkedGroups.Count);
            foreach (var linkedGroup in linkedGroups)
            {
                _logger.LogInformation("Getting status changes from incident group {IncidentGroupRowKey}.", linkedGroup.RowKey);
                if (!_filter.CanPostMessages(linkedGroup, cursor))
                {
                    _logger.LogInformation("Incident group did not pass filter. Cannot post messages about it.");
                    continue;
                }

                var path = linkedGroup.AffectedComponentPath;
                var status = (ComponentStatus)linkedGroup.AffectedComponentStatus;
                var startTime = linkedGroup.StartTime;
                _logger.LogInformation("Incident group started at {StartTime}.", startTime);
                events.Add(new MessageChangeEvent(startTime, path, status, MessageType.Start));
                if (!linkedGroup.IsActive)
                {
                    var endTime = linkedGroup.EndTime.Value;
                    _logger.LogInformation("Incident group ended at {EndTime}.", endTime);
                    events.Add(new MessageChangeEvent(endTime, path, status, MessageType.End));
                }
            }

            return events;
        }
    }
}