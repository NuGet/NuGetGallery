// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;

namespace NuGet.Protocol.Catalog
{
    public class SimpleHttpClientException : Exception
    {
        public SimpleHttpClientException(
            string message,
            HttpMethod method,
            string requestUri,
            HttpStatusCode statusCode,
            string reasonPhrase) : base(message)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
            RequestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase ?? throw new ArgumentNullException(nameof(reasonPhrase));
        }

        public HttpMethod Method { get; }
        public string RequestUri { get; }
        public HttpStatusCode StatusCode { get; }
        public string ReasonPhrase { get; }
    }
}
