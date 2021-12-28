// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    public class ActiveEventEntityUpdater : IActiveEventEntityUpdater
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<EventEntity> _updater;

        private readonly ILogger<ActiveEventEntityUpdater> _logger;

        public ActiveEventEntityUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<EventEntity> updater,
            ILogger<ActiveEventEntityUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAllAsync(DateTime cursor)
        {
            _logger.LogInformation("Updating active events.");
            var activeEvents = _table.GetActiveEntities<EventEntity>().ToList();
            _logger.LogInformation("Updating {ActiveEventsCount} active events.", activeEvents.Count());
            foreach (var activeEvent in activeEvents)
            {
                await _updater.UpdateAsync(activeEvent, cursor);
            }
        }
    }
}
