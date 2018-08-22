// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.V3PerPackage
{
    public class EnqueueCommand
    {
        private readonly EnqueueCollector _collector;

        public EnqueueCommand(EnqueueCollector collector)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        }

        public async Task ExecuteAsync(bool restart)
        {
            var fileSystemStorage = new FileStorageFactory(
                new Uri("http://localhost/"),
                Directory.GetCurrentDirectory(),
                verbose: false);

            var front = new DurableCursor(
                new Uri("http://localhost/cursor.json"),
                fileSystemStorage.Create(),
                DateTime.MinValue);

            if (restart)
            {
                await front.Load(CancellationToken.None);
                front.Value = DateTime.MinValue;
                await front.Save(CancellationToken.None);
            }

            var back = MemoryCursor.CreateMax();

            await _collector.Run(front, back, CancellationToken.None);
        }
    }
}