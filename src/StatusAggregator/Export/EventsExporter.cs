// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Export
{
    public class EventsExporter : IEventsExporter
    {
        private readonly TimeSpan _eventVisibilityPeriod;

        private readonly ITableWrapper _table;
        private readonly IEventExporter _exporter;

        private readonly ILogger<EventsExporter> _logger;

        public EventsExporter(
            ITableWrapper table,
            IEventExporter exporter,
            StatusAggregatorConfiguration configuration,
            ILogger<EventsExporter> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            _eventVisibilityPeriod = TimeSpan.FromDays(configuration?.EventVisibilityPeriodDays ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<Event> Export(DateTime cursor)
        {
            return _table
                .CreateQuery<EventEntity>()
                .Where(e => e.IsActive || (e.EndTime >= cursor - _eventVisibilityPeriod))
                .ToList()
                .Select(_exporter.Export)
                .Where(e => e != null)
                .ToList();
        }
    }
}