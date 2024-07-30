// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class HttpResponseException : Exception
    {
        public HttpResponseException(HttpStatusCode statusCode, string reasonPhrase, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }

        public HttpStatusCode StatusCode { get; }
        public string ReasonPhrase { get; }
    }
}
