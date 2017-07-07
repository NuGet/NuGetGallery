// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Web.Mvc;
using NuGet.Protocol;

namespace NuGetGallery
{
    public class HttpStatusCodeWithServerWarningResult : HttpStatusCodeResult
    {
        private readonly string _warningMessage;

        public HttpStatusCodeWithServerWarningResult(HttpStatusCode statusCode, string warningMessage)
            : base((int)statusCode)
        {
            _warningMessage = warningMessage;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.RequestContext.HttpContext.Response;

            if (!string.IsNullOrEmpty(_warningMessage) && !response.HeadersWritten)
            {
                response.AppendHeader(ProtocolConstants.ServerWarningHeader, _warningMessage);
            }

            base.ExecuteResult(context);
        }
    }
}