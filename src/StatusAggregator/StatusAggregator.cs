// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class StatusAggregator
    {
        private readonly CloudBlobContainer _container;
        private readonly ITableWrapper _table;

        private readonly IStatusUpdater _statusUpdater;
        private readonly IStatusExporter _statusExporter;

        public StatusAggregator(
            CloudBlobContainer container,
            ITableWrapper table,
            IStatusUpdater statusUpdater,
            IStatusExporter statusExporter)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _statusUpdater = statusUpdater ?? throw new ArgumentNullException(nameof(statusUpdater));
            _statusExporter = statusExporter ?? throw new ArgumentNullException(nameof(statusExporter));
        }

        public async Task Run()
        {
            await _table.CreateIfNotExistsAsync();
            await _container.CreateIfNotExistsAsync();

            await _statusUpdater.Update();
            await _statusExporter.Export();
        }
    }
}
