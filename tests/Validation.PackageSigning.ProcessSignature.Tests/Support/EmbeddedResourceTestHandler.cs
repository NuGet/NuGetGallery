// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class EmbeddedResourceTestHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<Uri, string> _urlToResourceName;

        public EmbeddedResourceTestHandler(IReadOnlyDictionary<Uri, string> urlToResourceName)
        {
            _urlToResourceName = urlToResourceName;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request));
        }

        private HttpResponseMessage Send(HttpRequestMessage request)
        {
            if (request.Method != HttpMethod.Get
                || !_urlToResourceName.TryGetValue(request.RequestUri, out var resourceName))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var resourceStream = TestResources.GetResourceStream(resourceName);
            if (resourceStream == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                RequestMessage = request,
                Content = new StreamContent(resourceStream),
            };
        }
    }
}
