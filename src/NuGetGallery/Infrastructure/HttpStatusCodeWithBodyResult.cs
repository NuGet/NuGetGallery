// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpStatusCodeWithBodyResult : HttpStatusCodeResult
    {
        public string Body { get; private set; }

        public HttpStatusCodeWithBodyResult(HttpStatusCode statusCode, string statusDescription)
            : this(statusCode, statusDescription, statusDescription)
        {
        }

        public HttpStatusCodeWithBodyResult(HttpStatusCode statusCode, string statusDescription, string body)
            : base((int)statusCode, statusDescription)
        {
            Body = body;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            base.ExecuteResult(context);
            var response = context.RequestContext.HttpContext.Response;
            response.Write(Body);
        }
    }
}