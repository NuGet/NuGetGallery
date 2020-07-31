// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;

namespace NgTests.Infrastructure
{
    /// <summary>
    /// Simple no-retry passthrough to avoid the full default exponential retry strategy in unit tests.
    /// </summary>
    public sealed class NoRetryStrategy : IHttpRetryStrategy
    {
        public Task<HttpResponseMessage> SendAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
        {
            return client.GetAsync(address, cancellationToken);
        }
    }
}