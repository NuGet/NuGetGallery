// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;

namespace NuGetGallery.Infrastructure
{
    public sealed class AntiForgeryErrorFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is HttpAntiForgeryException)
            {
                context.HttpContext.Response.Clear();
                context.HttpContext.Response.TrySkipIisCustomErrors = true;
                context.HttpContext.Response.StatusCode = 400;

                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Errors/CookieError.cshtml",
                };

                context.ExceptionHandled = true;
            }
        }
    }
}
