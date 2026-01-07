// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpStatusCodeWithHeadersResult : HttpStatusCodeResult
    {
        public readonly NameValueCollection Headers;

        public HttpStatusCodeWithHeadersResult(HttpStatusCode statusCode, NameValueCollection headers)
            : base(statusCode)
        {
            Headers = headers;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            base.ExecuteResult(context);
            var response = context.RequestContext.HttpContext.Response;
            response.Headers.Add(Headers);
        }
    }
}