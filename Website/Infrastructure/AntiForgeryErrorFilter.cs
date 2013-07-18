using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                    ViewName = "~/Errors/CookieError.cshtml",
                };

                context.ExceptionHandled = true;
            }
        }
    }
}
