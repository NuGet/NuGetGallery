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
