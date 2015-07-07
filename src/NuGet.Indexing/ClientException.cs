// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.Net;

namespace NuGet.Indexing
{
    public class ClientException : Exception
    {
        public ClientException(HttpStatusCode statusCode, string content)
        {
            StatusCode = statusCode;
            Content = (new JObject { { "error", content ?? string.Empty } }).ToString();
        }

        public ClientException(HttpStatusCode statusCode)
            : this(statusCode, null)
        {
        }

        public HttpStatusCode StatusCode { get; private set; }
        public string Content { get; private set; }
    }
}
