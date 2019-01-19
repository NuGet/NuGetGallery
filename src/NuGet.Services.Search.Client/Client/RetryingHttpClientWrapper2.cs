// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    public class RetryingHttpClientWrapper2 : IHttpClientWrapper
    {
        private const int RetryCount = 3;
        private readonly Action<Exception> _onException;

        public HttpClient Client { get; }

        /// <summary>
        /// Creates a RetryingHttpClientWrapper2 
        /// </summary>
        /// <param name="credentials">The network credentials.</param>
        /// <param name="onException">The action to be taken on exception.</param>
        /// <param name="handlers">The delegating handlers.</param>
        public RetryingHttpClientWrapper2(ICredentials credentials, Action<Exception> onException, params DelegatingHandler[] handlers)
        {
            _onException = onException ?? throw new ArgumentNullException(nameof(onException));
            HttpMessageHandler handler = new HttpRetryMessageHandler(new HttpClientHandler()
            {
                Credentials = credentials,
                AllowAutoRedirect = true,
                UseDefaultCredentials = credentials == null
            }, onException, RetryCount);

            foreach (var providedHandler in handlers.Reverse())
            {
                providedHandler.InnerHandler = handler;
                handler = providedHandler;
            }

            Client = new HttpClient(handler, disposeHandler: true);
        }

        public async Task<string> GetStringAsync(IEnumerable<Uri> endpoints)
        {
            return await Client.GetStringAsync(endpoints.First());
        }

        public async Task<HttpResponseMessage> GetAsync(IEnumerable<Uri> endpoints)
        {
            return await Client.GetAsync(endpoints.First());
        }

    }
}
