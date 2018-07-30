// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusUpdater : IStatusUpdater
    {
        private readonly ICursor _cursor;
        private readonly IIncidentUpdater _incidentUpdater;
        private readonly IEventUpdater _eventUpdater;

        private readonly ILogger<StatusUpdater> _logger;

        public StatusUpdater(
            ICursor cursor,
            IIncidentUpdater incidentUpdater,
            IEventUpdater eventUpdater,
            ILogger<StatusUpdater> logger)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _incidentUpdater = incidentUpdater ?? throw new ArgumentNullException(nameof(IncidentUpdater));
            _eventUpdater = eventUpdater ?? throw new ArgumentNullException(nameof(eventUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Update()
        {
            using (_logger.Scope("Updating service status."))
            {
                var lastCursor = await _cursor.Get();

                await _incidentUpdater.RefreshActiveIncidents();
                var nextCursor = await _incidentUpdater.FetchNewIncidents(lastCursor);

                await _eventUpdater.UpdateActiveEvents(nextCursor ?? DateTime.UtcNow);

                if (nextCursor.HasValue)
                {
                    await _cursor.Set(nextCursor.Value);
                }
            }
        }
    }
}
