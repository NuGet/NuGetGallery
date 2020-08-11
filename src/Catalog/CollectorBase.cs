// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CollectorBase
    {
        protected readonly ITelemetryService _telemetryService;
        private readonly Func<HttpMessageHandler> _handlerFunc;
        private readonly IHttpRetryStrategy _httpRetryStrategy;
        private readonly TimeSpan? _httpClientTimeout;

        public CollectorBase(
            Uri index,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null,
            IHttpRetryStrategy httpRetryStrategy = null)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _handlerFunc = handlerFunc;
            _httpClientTimeout = httpClientTimeout;
            _httpRetryStrategy = httpRetryStrategy;
            Index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public Uri Index { get; }

        public async Task<bool> RunAsync(CancellationToken cancellationToken)
        {
            return await RunAsync(MemoryCursor.CreateMin(), MemoryCursor.CreateMax(), cancellationToken);
        }

        public async Task<bool> RunAsync(DateTime front, DateTime back, CancellationToken cancellationToken)
        {
            return await RunAsync(new MemoryCursor(front), new MemoryCursor(back), cancellationToken);
        }

        public async Task<bool> RunAsync(ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken)
        {
            await Task.WhenAll(front.LoadAsync(cancellationToken), back.LoadAsync(cancellationToken));

            Trace.TraceInformation("Run ( {0} , {1} )", front, back);

            bool result = false;

            HttpMessageHandler handler = null;

            if (_handlerFunc != null)
            {
                handler = _handlerFunc();
            }

            using (CollectorHttpClient client = new CollectorHttpClient(handler, _httpRetryStrategy))
            {
                if (_httpClientTimeout.HasValue)
                {
                    client.Timeout = _httpClientTimeout.Value;
                }

                result = await FetchAsync(client, front, back, cancellationToken);
            }

            return result;
        }

        protected abstract Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken);
    }
}