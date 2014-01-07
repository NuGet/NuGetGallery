using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http.Filters;

namespace NuGet.Services.Http
{
    public class RecordApiExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext actionExecutedContext)
        {
            ServicePlatformEventSource.Log.ApiException(
                actionExecutedContext.Request.RequestUri.AbsoluteUri,
                actionExecutedContext.ActionContext.ActionDescriptor.ControllerDescriptor.ControllerName,
                actionExecutedContext.ActionContext.ActionDescriptor.ActionName,
                actionExecutedContext.Exception);
            base.OnException(actionExecutedContext);
        }
    }
}
