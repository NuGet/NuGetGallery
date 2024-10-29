// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StatusAggregator.Collector;
using StatusAggregator.Update;

namespace StatusAggregator.Export
{
    public class StatusExporter : IStatusExporter
    {
        private readonly ICursor _cursor;
        private readonly IComponentExporter _componentExporter;
        private readonly IEventsExporter _eventExporter;
        private readonly IStatusSerializer _serializer;

        private readonly ILogger<StatusExporter> _logger;

        public StatusExporter(
            ICursor cursor,
            IComponentExporter componentExporter,
            IEventsExporter eventExporter,
            IStatusSerializer serializer,
            ILogger<StatusExporter> logger)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _componentExporter = componentExporter ?? throw new ArgumentNullException(nameof(componentExporter));
            _eventExporter = eventExporter ?? throw new ArgumentNullException(nameof(eventExporter));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Export(DateTime cursor)
        {
            _logger.LogInformation("Exporting service status.");
            var rootComponent = _componentExporter.Export();
            var recentEvents = _eventExporter.Export(cursor);

            var lastUpdated = await _cursor.Get(StatusUpdater.LastUpdatedCursorName);
            await _serializer.Serialize(cursor, lastUpdated, rootComponent, recentEvents);
        }
    }
}