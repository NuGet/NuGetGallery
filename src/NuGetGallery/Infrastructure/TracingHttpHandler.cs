// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Infrastructure
{
    public class TracingHttpHandler : DelegatingHandler
    {
        public IDiagnosticsSource Trace { get; private set; }

        public TracingHttpHandler(IDiagnosticsSource trace)
        {
            Trace = trace;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp;
            long milliseconds = 0;
            using (Trace.Activity(request.Method + " " + request.RequestUri.AbsoluteUri))
            {
                Stopwatch sw = Stopwatch.StartNew();
                resp = await base.SendAsync(request, cancellationToken);
                sw.Stop();
                milliseconds = sw.ElapsedMilliseconds;
            }

            string message = ((int)resp.StatusCode).ToString() + " " + request.RequestUri.AbsoluteUri + " " + milliseconds;
            if (resp.IsSuccessStatusCode)
            {
                Trace.Information(message);
            } else {
                Trace.Error(message);
            }

            return resp;
        }
    }
}
