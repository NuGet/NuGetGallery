using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Web.Http.Filters;

namespace NuGet.Services.Http.Filters
{
    public class ApiExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            ServicePlatformEventSource.Log.ApiException(
                actionExecutedContext.Request.RequestUri.AbsoluteUri,
                actionExecutedContext.ActionContext.ActionDescriptor.ControllerDescriptor.ControllerName,
                actionExecutedContext.ActionContext.ActionDescriptor.ActionName,
                actionExecutedContext.Exception);
            var requestContext = actionExecutedContext.Request.GetRequestContext();
            if (requestContext != null && requestContext.Principal != null && requestContext.Principal.IsInRole(Roles.Admin))
            {
                requestContext.IncludeErrorDetail = true;
            }
            base.OnException(actionExecutedContext);
        }
    }
}
