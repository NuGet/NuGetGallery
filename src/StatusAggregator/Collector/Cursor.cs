// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Collector
{
    public class Cursor : ICursor
    {
        private readonly ITableWrapper _table;
        private readonly ILogger<Cursor> _logger;

        public Cursor(
            ITableWrapper table,
            ILogger<Cursor> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DateTime> Get(string name)
        {
            name = name ?? throw new ArgumentNullException(nameof(name));
            _logger.LogInformation("Fetching cursor with name {CursorName}.", name);
            var cursor = await _table.RetrieveAsync<CursorEntity>(name);

            DateTime value;
            if (cursor == null)
            {
                // If we can't find a cursor, the job is likely uninitialized, so start at the beginning of time.
                value = DateTime.MinValue;
                _logger.LogInformation("Could not fetch cursor, reinitializing cursor at {CursorValue}.", value);
            }
            else
            {
                value = cursor.Value;
                _logger.LogInformation("Fetched cursor with value {CursorValue}.", value);
            }

            return value;
        }

        public Task Set(string name, DateTime value)
        {
            name = name ?? throw new ArgumentNullException(nameof(name));
            _logger.LogInformation("Updating cursor with name {CursorName} to {CursorValue}.", name, value);
            var cursorEntity = new CursorEntity(name, value);
            return _table.InsertOrReplaceAsync(cursorEntity);
        }
    }
}