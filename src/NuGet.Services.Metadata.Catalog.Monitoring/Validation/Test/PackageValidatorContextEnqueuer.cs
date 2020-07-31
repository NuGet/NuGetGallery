// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Fetches and enqueues the <see cref="PackageValidatorContext"/>s  associated with recent catalog entries to be processed by <see cref="PackageValidator"/>.
    /// </summary>
    public class PackageValidatorContextEnqueuer
    {
        private readonly ValidationCollector _collector;
        private readonly ReadWriteCursor _front;
        private readonly ReadCursor _back;

        public PackageValidatorContextEnqueuer(
            ValidationCollector collector,
            ReadWriteCursor front,
            ReadCursor back)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
            _front = front ?? throw new ArgumentNullException(nameof(front));
            _back = back ?? throw new ArgumentNullException(nameof(back));
        }

        public async Task EnqueuePackageValidatorContexts(CancellationToken token)
        {
            bool run;
            do
            {
                run = await _collector.RunAsync(_front, _back, token);
            }
            while (run);
        }
    }
}
