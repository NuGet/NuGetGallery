// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.TestUtils
{
    public class MockHandler
        : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunction;

        public MockHandler()
        {
            _handlerFunction = (r, c) => Return200();
        }

        public MockHandler(Func<HttpRequestMessage,
            CancellationToken, Task<HttpResponseMessage>> handlerFunction)
        {
            _handlerFunction = handlerFunction;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handlerFunction(request, cancellationToken);
        }

        public static Task<HttpResponseMessage> Return200()
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}