// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;

namespace NuGet.Services.AzureSearch
{
    public class ServiceClientTracingLogger : IServiceClientTracingInterceptor
    {
        private const string Prefix = "ServiceClient ";
        private readonly ILogger<ServiceClientTracingLogger> _logger;

        public ServiceClientTracingLogger(ILogger<ServiceClientTracingLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SendRequest(string invocationId, HttpRequestMessage request)
        {
            _logger.LogInformation(
                Prefix + "invocation {InvocationId} sending request: {Method} {RequestUri}",
                invocationId,
                request.Method,
                request.RequestUri);
        }

        public void ReceiveResponse(string invocationId, HttpResponseMessage response)
        {
            _logger.LogInformation(
                Prefix + "invocation {InvocationId} received response: {StatusCode} {ReasonPhrase}",
                invocationId,
                (int)response.StatusCode,
                response.ReasonPhrase);
        }

        public void Configuration(string source, string name, string value)
        {
        }

        public void EnterMethod(string invocationId, object instance, string method, IDictionary<string, object> parameters)
        {
        }

        public void ExitMethod(string invocationId, object returnValue)
        {
        }

        public void Information(string message)
        {
        }

        public void TraceError(string invocationId, Exception exception)
        {
        }
    }
}
