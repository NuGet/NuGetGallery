// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CollectorBase
    {
        protected readonly ITelemetryService _telemetryService;
        private readonly Func<HttpMessageHandler> _handlerFunc;
        private readonly TimeSpan? _httpClientTimeout;

        public CollectorBase(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _handlerFunc = handlerFunc;
            _httpClientTimeout = httpClientTimeout;
            Index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public Uri Index { get; private set; }

        public int RequestCount { get; private set; }

        public async Task<bool> Run(CancellationToken cancellationToken)
        {
            return await Run(MemoryCursor.CreateMin(), MemoryCursor.CreateMax(), cancellationToken);
        }

        public async Task<bool> Run(DateTime front, DateTime back, CancellationToken cancellationToken)
        {
            return await Run(new MemoryCursor(front), new MemoryCursor(back), cancellationToken);
        }

        public async Task<bool> Run(ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken)
        {
            await Task.WhenAll(front.Load(cancellationToken), back.Load(cancellationToken));

            Trace.TraceInformation("Run ( {0} , {1} )", front, back);

            bool result = false;

            HttpMessageHandler handler = null;

            if (_handlerFunc != null)
            {
                handler = _handlerFunc();
            }

            using (CollectorHttpClient client = new CollectorHttpClient(handler))
            {
                if (_httpClientTimeout.HasValue)
                {
                    client.Timeout = _httpClientTimeout.Value;
                }

                result = await Fetch(client, front, back, cancellationToken);
                RequestCount = client.RequestCount;
            }
            
            return result;
        }

        protected abstract Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken);
    }
}
