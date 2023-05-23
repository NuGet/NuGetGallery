// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class RequiresUserAgentAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            if (string.IsNullOrWhiteSpace(filterContext.HttpContext.GetUserAgent()))
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.BadRequest,
                    statusDescription: "User-Agent header is required",
                    body: "A User-Agent header is required for this endpoint.");
            }
        }
    }
}