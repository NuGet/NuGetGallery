// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StatusAggregator.Container;
using StatusAggregator.Export;
using StatusAggregator.Table;
using StatusAggregator.Update;

namespace StatusAggregator
{
    public class StatusAggregator
    {
        private readonly IEnumerable<IContainerWrapper> _containers;
        private readonly IEnumerable<ITableWrapper> _tables;

        private readonly IStatusUpdater _statusUpdater;
        private readonly IStatusExporter _statusExporter;

        public StatusAggregator(
            IEnumerable<IContainerWrapper> containers,
            IEnumerable<ITableWrapper> tables,
            IStatusUpdater statusUpdater,
            IStatusExporter statusExporter)
        {
            _containers = containers ?? throw new ArgumentNullException(nameof(containers));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _statusUpdater = statusUpdater ?? throw new ArgumentNullException(nameof(statusUpdater));
            _statusExporter = statusExporter ?? throw new ArgumentNullException(nameof(statusExporter));
        }

        public async Task Run(DateTime cursor)
        {
            // Initialize all tables and containers.
            await Task.WhenAll(_tables.Select(t => t.CreateIfNotExistsAsync()));
            await Task.WhenAll(_containers.Select(c => c.CreateIfNotExistsAsync()));
            
            // Update and export the status.
            await _statusUpdater.Update(cursor);
            await _statusExporter.Export(cursor);
        }
    }
}
