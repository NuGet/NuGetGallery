// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;

namespace NuGetGallery.Infrastructure
{
    public sealed class UserDeletedErrorFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is CurrentUserDeletedException)
            {
                context.HttpContext.Response.Clear();
                context.HttpContext.Response.TrySkipIisCustomErrors = true;

                context.Controller.TempData = new TempDataDictionary
                {
                    { "Message", Strings.LoggedInUserDeleted }
                };

                context.Result = new RedirectResult("/");

                context.ExceptionHandled = true;
            }
        }
    }
}