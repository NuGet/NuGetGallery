// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.SearchClient
{
    public class RequestInspectingHandler
        : DelegatingHandler
    {
        public List<HttpRequestMessage> Requests { get; private set; }

        public RequestInspectingHandler()
        {
            Requests = new List<HttpRequestMessage>();
            InnerHandler = new HttpClientHandler();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return base.SendAsync(request, cancellationToken);
        }
    }
}