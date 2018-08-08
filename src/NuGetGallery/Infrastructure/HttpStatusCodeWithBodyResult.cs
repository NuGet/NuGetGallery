// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpStatusCodeWithBodyResult : HttpStatusCodeResult
    {
        private static readonly string[] LineEndings = new[] { "\n", "\r" };

        public string Body { get; private set; }

        public HttpStatusCodeWithBodyResult(HttpStatusCode statusCode, string statusDescription)
            : this(statusCode, statusDescription, statusDescription)
        {
        }

        public HttpStatusCodeWithBodyResult(HttpStatusCode statusCode, string statusDescription, string body)
            : base((int)statusCode, ConvertToSingleLine(statusDescription))
        {
            Body = body;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            base.ExecuteResult(context);
            var response = context.RequestContext.HttpContext.Response;
            response.Write(Body);
        }

        private static string ConvertToSingleLine(string reasonPhrase)
        {
            if (reasonPhrase == null || LineEndings.All(x => !reasonPhrase.Contains(x)))
            {
                return reasonPhrase;
            }

            var lines = reasonPhrase
                .Split(LineEndings, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            return string.Join(" ", lines);
        }
    }
}