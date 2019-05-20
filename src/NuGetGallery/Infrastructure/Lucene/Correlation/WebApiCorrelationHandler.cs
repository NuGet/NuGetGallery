// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Hosting;

namespace NuGetGallery.Infrastructure.Search.Correlation
{
    public class WebApiCorrelationHandler 
        : DelegatingHandler
    {
        public static string CallContextKey = "CorrelatingHttpHandler_CID";
        public static string CorrelationIdHttpHeaderName = "X-CorrelationId";
        
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Determine correlation id
            DetermineCorrelationId(request);

            // Run all the things
            var response = await base.SendAsync(request, cancellationToken);

            // Set the correlation id on the response
            SetResponseCorrelationId(request, response);

            return response;
        }

        private void DetermineCorrelationId(HttpRequestMessage request)
        {
            if (request != null)
            {
                // Ensure we have a correlation id on the request context / use Web API's MS_RequestId header
                var correlationId = request.GetCorrelationId();

                // Does the client override things?
                if (request.Headers.Contains(CorrelationIdHttpHeaderName))
                {
                    var temp = request.Headers
                        .GetValues(CorrelationIdHttpHeaderName)
                        .FirstOrDefault(s => s != null);
                    if (Guid.TryParse(temp, out correlationId))
                    {
                        // Overwrite the correlation id from Web API's MS_RequestId header
                        if (request.Properties.ContainsKey(HttpPropertyKeys.RequestCorrelationKey))
                        {
                            request.Properties.Remove(HttpPropertyKeys.RequestCorrelationKey);
                        }

                        request.Properties.Add(HttpPropertyKeys.RequestCorrelationKey, correlationId);
                    }
                }

                // Set the correlation id on the logical call context so HttpClients using
                // the CorrelatingHttpClientHandler can pass it along.
                CallContext.LogicalSetData(CallContextKey, correlationId);
            }
        }

        private void SetResponseCorrelationId(HttpRequestMessage request, HttpResponseMessage response)
        {
            if (response != null)
            {
                // Do not allow overriding the header - if any code wants to set the correlation id
                // it should set the request property instead.
                if (response.Headers.Contains(CorrelationIdHttpHeaderName))
                {
                    response.Headers.Remove(CorrelationIdHttpHeaderName);
                }

                // Set correlation id header on the response
                response.Headers.Add(CorrelationIdHttpHeaderName, request.GetCorrelationId().ToString("D"));
            }
        }
    }
}