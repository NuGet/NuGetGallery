// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;

namespace NuGetGallery
{
    public class StagingApiException : Exception
    {
        public StagingApiException(HttpStatusCode statusCode, string code, string message, string target = null)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Target = target;
        }

        public HttpStatusCode StatusCode { get; }
        public string Code { get; }
        public string Target { get; }
    }
}
