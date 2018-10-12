// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using StatusAggregator.Manual;
using StatusAggregator.Update;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusUpdater : IStatusUpdater
    {
        private const string ManualCursorBaseName = "manual";
        private const string IncidentCursorName = "incident";

        private readonly ICursor _cursor;
        private readonly IEnumerable<IManualStatusChangeUpdater> _manualStatusChangeUpdaters;
        private readonly IIncidentUpdater _incidentUpdater;
        private readonly IActiveEventEntityUpdater _activeEventUpdater;

        private readonly ILogger<StatusUpdater> _logger;

        public StatusUpdater(
            ICursor cursor,
            IEnumerable<IManualStatusChangeUpdater> manualStatusChangeUpdaters,
            IIncidentUpdater incidentUpdater,
            IActiveEventEntityUpdater eventUpdater,
            ILogger<StatusUpdater> logger)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _manualStatusChangeUpdaters = manualStatusChangeUpdaters ?? throw new ArgumentNullException(nameof(manualStatusChangeUpdaters));
            _incidentUpdater = incidentUpdater ?? throw new ArgumentNullException(nameof(incidentUpdater));
            _activeEventUpdater = eventUpdater ?? throw new ArgumentNullException(nameof(eventUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Update()
        {
            using (_logger.Scope("Updating service status."))
            {
                foreach (var manualStatusChangeUpdater in _manualStatusChangeUpdaters)
                {
                    await ProcessCursor($"{ManualCursorBaseName}{manualStatusChangeUpdater.Name}", manualStatusChangeUpdater.ProcessNewManualChanges);
                }

                var incidentCursor = await ProcessCursor(IncidentCursorName, async (value) =>
                {
                    return await _incidentUpdater.FetchNewIncidents(value);
                });

                await _activeEventUpdater.UpdateAllAsync(incidentCursor);
            }
        }

        private async Task<DateTime> ProcessCursor(string name, Func<DateTime, Task<DateTime?>> processCursor)
        {
            var lastCursor = await _cursor.Get(name);
            var nextCursor = await processCursor(lastCursor);
            if (nextCursor.HasValue)
            {
                await _cursor.Set(name, nextCursor.Value);
            }

            return nextCursor ?? lastCursor;
        }
    }
}
