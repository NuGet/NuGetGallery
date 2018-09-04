// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class ManualStatusChangeUpdater : IManualStatusChangeUpdater
    {
        private readonly ITableWrapper _table;
        private readonly IManualStatusChangeHandler _handler;
        private readonly ILogger<ManualStatusChangeUpdater> _logger;

        public ManualStatusChangeUpdater(
            string name,
            ITableWrapper table,
            IManualStatusChangeHandler handler,
            ILogger<ManualStatusChangeUpdater> logger)
        {
            Name = name;
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name { get; }

        public async Task<DateTime?> ProcessNewManualChanges(DateTime cursor)
        {
            using (_logger.Scope("Processing manual status changes."))
            {
                var manualChangesQuery = _table
                    .CreateQuery<ManualStatusChangeEntity>()
                    .Where(c => c.PartitionKey == ManualStatusChangeEntity.DefaultPartitionKey);

                // Table storage throws on queries with DateTime values that are too low.
                // If we are fetching manual changes for the first time, don't filter on the timestamp.
                if (cursor > DateTime.MinValue)
                {
                    manualChangesQuery = manualChangesQuery.Where(c => c.Timestamp > new DateTimeOffset(cursor, TimeSpan.Zero));
                }
                
                var manualChanges = manualChangesQuery.ToList();

                _logger.LogInformation("Processing {ManualChangesCount} manual status changes.", manualChanges.Count());
                foreach (var manualChange in manualChanges)
                {
                    await _handler.Handle(_table, manualChange);
                }
                
                return manualChanges.Any() ? manualChanges.Max(c => c.Timestamp.UtcDateTime) : (DateTime?)null;
            }
        }
    }
}
