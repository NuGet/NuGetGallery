﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Infrastructure
{
    public class TracingHttpHandler : DelegatingHandler
    {
        public IDiagnosticsSource Trace { get; }

        public TracingHttpHandler(IDiagnosticsSource trace)
        {
            Trace = trace;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage resp;
            using (Trace.Activity(request.Method + " " + request.RequestUri.AbsoluteUri))
            {
                resp = await base.SendAsync(request, cancellationToken);
            }

            string message = ((int)resp.StatusCode).ToString() + " " + request.RequestUri.AbsoluteUri;
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
