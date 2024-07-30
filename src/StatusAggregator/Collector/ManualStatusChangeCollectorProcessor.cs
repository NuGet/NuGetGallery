// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
using StatusAggregator.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator.Collector
{
    /// <summary>
    /// Fetches new <see cref="ManualStatusChangeEntity"/>s using an <see cref="ITableWrapper"/>.
    /// </summary>
    public class ManualStatusChangeCollectorProcessor : IEntityCollectorProcessor
    {
        public const string ManualCollectorNamePrefix = "manual";

        private readonly ITableWrapper _table;
        private readonly IManualStatusChangeHandler _handler;
        private readonly ILogger<ManualStatusChangeCollectorProcessor> _logger;

        public ManualStatusChangeCollectorProcessor(
            string name,
            ITableWrapper table,
            IManualStatusChangeHandler handler,
            ILogger<ManualStatusChangeCollectorProcessor> logger)
        {
            Name = ManualCollectorNamePrefix + 
                (name ?? throw new ArgumentNullException(nameof(name)));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name { get; }

        public async Task<DateTime?> FetchSince(DateTime cursor)
        {
            _logger.LogInformation("Processing manual status changes.");

            var manualChangesQuery = _table
                .CreateQuery<ManualStatusChangeEntity>();

            // Table storage throws on queries with DateTime values that are too low.
            // If we are fetching manual changes for the first time, don't filter on the timestamp.
            if (cursor > DateTime.MinValue)
            {
                manualChangesQuery = manualChangesQuery.Where(c => c.Timestamp > new DateTimeOffset(cursor, TimeSpan.Zero));
            }

            var manualChanges = manualChangesQuery.ToList();

            _logger.LogInformation("Processing {ManualChangesCount} manual status changes.", manualChanges.Count());
            foreach (var manualChange in manualChanges.OrderBy(m => m.Timestamp))
            {
                await _handler.Handle(_table, manualChange);
            }

            return manualChanges.Any() ? manualChanges.Max(c => c.Timestamp.UtcDateTime) : (DateTime?)null;
        }
    }
}