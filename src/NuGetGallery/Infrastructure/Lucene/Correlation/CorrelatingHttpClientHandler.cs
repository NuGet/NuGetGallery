// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Infrastructure.Search.Correlation
{
    /// <summary>
    /// Attaches correlation id to outgoing HTTP requests when using HttpClient.
    /// </summary>
    public class CorrelatingHttpClientHandler 
        : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = CallContext.LogicalGetData(WebApiCorrelationHandler.CallContextKey);
            if (correlationId != null)
            {
                if (!request.Headers.Contains(WebApiCorrelationHandler.CorrelationIdHttpHeaderName))
                {
                    request.Headers.Add(
                        WebApiCorrelationHandler.CorrelationIdHttpHeaderName, 
                        correlationId.ToString());
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}