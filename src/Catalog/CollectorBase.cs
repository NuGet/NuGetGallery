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
        Func<HttpMessageHandler> _handlerFunc;

        public CollectorBase(Uri index, Func<HttpMessageHandler> handlerFunc = null)
        {
            _handlerFunc = handlerFunc;
            Index = index;
            ServicePointManager.DefaultConnectionLimit = 4;
            ServicePointManager.MaxServicePointIdleTime = 10000;
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
                result = await Fetch(client, front, back, cancellationToken);
                RequestCount = client.RequestCount;
            }
            
            return result;
        }

        protected abstract Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken);
    }
}
