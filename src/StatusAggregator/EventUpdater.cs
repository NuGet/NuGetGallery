// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class EventUpdater : IEventUpdater
    {
        public readonly TimeSpan _eventEndDelay;

        private readonly ITableWrapper _table;
        private readonly IMessageUpdater _messageUpdater;

        private readonly ILogger<EventUpdater> _logger;

        public EventUpdater(
            ITableWrapper table, 
            IMessageUpdater messageUpdater, 
            StatusAggregatorConfiguration configuration,
            ILogger<EventUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _messageUpdater = messageUpdater ?? throw new ArgumentNullException(nameof(messageUpdater));
            _eventEndDelay = TimeSpan.FromMinutes(configuration?.EventEndDelayMinutes ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateActiveEvents(DateTime cursor)
        {
            using (_logger.Scope("Updating active events."))
            {
                var activeEvents = _table.GetActiveEvents().ToList();
                _logger.LogInformation("Updating {ActiveEventsCount} active events.", activeEvents.Count());
                foreach (var activeEvent in activeEvents)
                {
                    await UpdateEvent(activeEvent, cursor);
                }
            }
        }

        public async Task<bool> UpdateEvent(EventEntity eventEntity, DateTime cursor)
        {
            eventEntity = eventEntity ?? throw new ArgumentNullException(nameof(eventEntity));

            using (_logger.Scope("Updating event '{EventRowKey}' given cursor {Cursor}.", eventEntity.RowKey, cursor))
            {
                if (!eventEntity.IsActive)
                {
                    _logger.LogInformation("Event is inactive, cannot update.");
                    return false;
                }

                var incidentsLinkedToEventQuery = _table.GetIncidentsLinkedToEvent(eventEntity);

                var incidentsLinkedToEvent = incidentsLinkedToEventQuery.ToList();
                if (!incidentsLinkedToEvent.Any())
                {
                    _logger.LogInformation("Event has no linked incidents and must have been created manually, cannot update.");
                    return false;
                }

                // We are querying twice here because table storage ignores rows where a column specified by a query is null.
                // MitigationTime is null when IsActive is true.
                // If we do not query separately here, rows where IsActive is true will be ignored in query results.

                var hasActiveIncidents = incidentsLinkedToEventQuery
                    .Where(i => i.IsActive)
                    .ToList()
                    .Any();

                var hasRecentIncidents = incidentsLinkedToEventQuery
                    .Where(i => i.MitigationTime > cursor - _eventEndDelay)
                    .ToList()
                    .Any();

                var shouldDeactivate = !(hasActiveIncidents || hasRecentIncidents);
                if (shouldDeactivate)
                {
                    _logger.LogInformation("Deactivating event because its incidents are inactive and too old.");
                    var mitigationTime = incidentsLinkedToEvent
                        .Max(i => i.MitigationTime ?? DateTime.MinValue);
                    eventEntity.EndTime = mitigationTime;

                    await _messageUpdater.CreateMessageForEventStart(eventEntity, mitigationTime);
                    await _messageUpdater.CreateMessageForEventEnd(eventEntity);

                    // Update the event
                    await _table.InsertOrReplaceAsync(eventEntity);
                }
                else
                {
                    _logger.LogInformation("Event has active or recent incidents so it will not be deactivated.");
                    await _messageUpdater.CreateMessageForEventStart(eventEntity, cursor);
                }

                return shouldDeactivate;
            }
        }
    }
}
