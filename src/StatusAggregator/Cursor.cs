// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class Cursor : ICursor
    {
        public Cursor(
            ITableWrapper table,
            ILogger<Cursor> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private readonly ITableWrapper _table;

        private readonly ILogger<Cursor> _logger;

        public async Task<DateTime> Get()
        {
            using (_logger.Scope("Fetching cursor."))
            {
                var cursor = await _table.Retrieve<CursorEntity>(
                    CursorEntity.DefaultPartitionKey, CursorEntity.DefaultRowKey);

                DateTime value;
                if (cursor == null)
                {
                    // If we can't find a cursor, the job is likely uninitialized, so start at the beginning of time.
                    value = DateTime.MinValue;
                    _logger.LogInformation("Could not fetch cursor, reinitializing cursor at {Cursor}.", value);
                }
                else
                {
                    value = cursor.Value;
                    _logger.LogInformation("Fetched cursor with value {Cursor}.", value);
                }

                return value;
            }
        }

        public Task Set(DateTime value)
        {
            using (_logger.Scope("Updating cursor to {Cursor}.", value))
            {
                var cursorEntity = new CursorEntity(value);
                return _table.InsertOrReplaceAsync(cursorEntity);
            }
        }
    }
}
