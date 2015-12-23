﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;

namespace NuGetGallery.Infrastructure
{
    internal sealed class ReadOnlyModeErrorFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is ReadOnlyModeException)
            {
                context.HttpContext.Response.Clear();
                context.HttpContext.Response.TrySkipIisCustomErrors = true;
                context.HttpContext.Response.StatusCode = 503;

                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Errors/ReadOnlyMode.cshtml",
                };

                context.ExceptionHandled = true;
            }
        }
    }
}