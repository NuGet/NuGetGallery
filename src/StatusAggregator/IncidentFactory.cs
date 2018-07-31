// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentFactory : IIncidentFactory
    {
        private readonly ITableWrapper _table;
        private readonly IEventUpdater _eventUpdater;

        private readonly ILogger<IncidentFactory> _logger;

        public IncidentFactory(
            ITableWrapper table, 
            IEventUpdater eventUpdater, 
            ILogger<IncidentFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventUpdater = eventUpdater ?? throw new ArgumentNullException(nameof(eventUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IncidentEntity> CreateIncident(ParsedIncident parsedIncident)
        {
            var incidentEntity = new IncidentEntity(
                parsedIncident.Id,
                parsedIncident.AffectedComponentPath,
                parsedIncident.AffectedComponentStatus,
                parsedIncident.CreationTime,
                parsedIncident.MitigationTime);

            using (_logger.Scope("Creating incident '{IncidentRowKey}'.", incidentEntity.RowKey))
            {
                // Find an event to attach this incident to
                var possibleEvents = _table
                    .CreateQuery<EventEntity>()
                    .Where(e =>
                        e.PartitionKey == EventEntity.DefaultPartitionKey &&
                        // The incident and the event must affect the same component
                        e.AffectedComponentPath == incidentEntity.AffectedComponentPath &&
                        // The event must begin before or at the same time as the incident
                        e.StartTime <= incidentEntity.CreationTime &&
                        // The event must be active or the event must end after this incident begins
                        (e.IsActive || (e.EndTime >= incidentEntity.CreationTime)))
                    .ToList();

                _logger.LogInformation("Found {EventCount} possible events to link incident to.", possibleEvents.Count());
                EventEntity eventToLinkTo = null;
                foreach (var possibleEventToLinkTo in possibleEvents)
                {
                    if (!_table.GetIncidentsLinkedToEvent(possibleEventToLinkTo).ToList().Any())
                    {
                        _logger.LogInformation("Cannot link incident to event '{EventRowKey}' because it is not linked to any incidents.", possibleEventToLinkTo.RowKey);
                        continue;
                    }

                    if (await _eventUpdater.UpdateEvent(possibleEventToLinkTo, incidentEntity.CreationTime))
                    {
                        _logger.LogInformation("Cannot link incident to event '{EventRowKey}' because it has been deactivated.", possibleEventToLinkTo.RowKey);
                        continue;
                    }

                    _logger.LogInformation("Linking incident to event '{EventRowKey}'.", possibleEventToLinkTo.RowKey);
                    eventToLinkTo = possibleEventToLinkTo;
                    break;
                }

                if (eventToLinkTo == null)
                {
                    eventToLinkTo = new EventEntity(incidentEntity);
                    _logger.LogInformation("Could not find existing event to link to, creating new event '{EventRowKey}' to link incident to.", eventToLinkTo.RowKey);
                    await _table.InsertOrReplaceAsync(eventToLinkTo);
                }

                incidentEntity.EventRowKey = eventToLinkTo.RowKey;
                await _table.InsertOrReplaceAsync(incidentEntity);

                if ((int)parsedIncident.AffectedComponentStatus > eventToLinkTo.AffectedComponentStatus)
                {
                    _logger.LogInformation("Increasing severity of event '{EventRowKey}' because newly linked incident is more severe than the event.", eventToLinkTo.RowKey);
                    eventToLinkTo.AffectedComponentStatus = (int)parsedIncident.AffectedComponentStatus;
                    await _table.InsertOrReplaceAsync(eventToLinkTo);
                }

                return incidentEntity;
            }
        }
    }
}
