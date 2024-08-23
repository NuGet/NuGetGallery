// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NgTests.Infrastructure
{
    public class MockServerHttpClientHandler
        : HttpClientHandler
    {
        public Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> Actions { get; private set; }
        public bool Return404OnUnknownAction { get; set; }

        private readonly object _requestsLock = new object();
        private readonly List<HttpRequestMessage> _requests = new List<HttpRequestMessage>();

        public MockServerHttpClientHandler()
        {
            Actions = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>();
            Return404OnUnknownAction = true;
        }

        public void SetAction(string relativeUrl, Func<HttpRequestMessage, Task<HttpResponseMessage>> action)
        {
            Actions[relativeUrl] = action;
        }

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_requestsLock)
            {
                _requests.Add(request);
            }

            Func<HttpRequestMessage, Task<HttpResponseMessage>> action;

            // try with full URL
            if (Actions.TryGetValue(request.RequestUri.PathAndQuery, out action))
            {
                return await action(request);
            }

            // try with full URL ignoring query string
            if (Actions.TryGetValue(request.RequestUri.AbsolutePath, out action))
            {
                return await action(request);
            }

            if (Return404OnUnknownAction)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Could not find " + request.RequestUri)
                };
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}