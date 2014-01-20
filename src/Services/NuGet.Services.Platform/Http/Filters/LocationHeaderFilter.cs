using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Autofac.Integration.WebApi;

namespace NuGet.Services.Http.Filters
{
    public class LocationHeaderFilter : IAutofacActionFilter
    {
        public void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            actionExecutedContext.Response.Headers.Add("Location", actionExecutedContext.Request.RequestUri.AbsoluteUri);
        }

        public void OnActionExecuting(HttpActionContext actionContext)
        {
        }
    }
}
