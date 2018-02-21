// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class VerboseHandler : DelegatingHandler
    {
        public VerboseHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            sw.Stop();
            Trace.TraceInformation("HTTP {0}_{1}_{2}", request.Method, request.RequestUri, sw.ElapsedMilliseconds);
            return response;
        }
    }
}
