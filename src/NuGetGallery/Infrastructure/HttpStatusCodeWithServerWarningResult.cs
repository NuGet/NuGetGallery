// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpStatusCodeWithServerWarningResult : HttpStatusCodeResult
    {
        public IReadOnlyList<string> Warnings { get; }

        public HttpStatusCodeWithServerWarningResult(HttpStatusCode statusCode, IReadOnlyList<string> warnings)
            : base((int)statusCode)
        {
            Warnings = warnings ?? Array.Empty<string>();
        }

        public HttpStatusCodeWithServerWarningResult(HttpStatusCode statusCode, IReadOnlyList<IValidationMessage> warnings)
            : this(statusCode, warnings.Select(w => w.PlainTextMessage).ToList())
        {
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.RequestContext.HttpContext.Response;

            if (Warnings.Any() && !response.HeadersWritten)
            {
                foreach (var warning in Warnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning))
                    {
                        response.AppendHeader(GalleryConstants.WarningHeaderName, warning);
                    }
                }
            }

            base.ExecuteResult(context);
        }
    }
}