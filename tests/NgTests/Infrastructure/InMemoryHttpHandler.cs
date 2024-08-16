// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NgTests.Infrastructure
{
    public class InMemoryHttpHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responses;

        public InMemoryHttpHandler(IReadOnlyDictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage responseMessage;
            string response;
            if (_responses.TryGetValue(request.RequestUri.ToString(), out response))
            {
                responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response)
                };
            }
            else
            {
                responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Task.FromResult(responseMessage);
        }
    }
}