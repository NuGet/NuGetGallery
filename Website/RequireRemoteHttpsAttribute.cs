using System;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    // This code is identical to System.Web.Mvc except that we allow for working in localhost environment without https.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class RequireRemoteHttpsAttribute : FilterAttribute, IAuthorizationFilter
    {
        public virtual void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            var request = filterContext.HttpContext.Request;
            if (!request.IsLocal && !request.IsSecureConnection)
            {
                HandleNonHttpsRequest(filterContext);
            }
        }

        protected virtual void HandleNonHttpsRequest(AuthorizationContext filterContext)
        {
            // only redirect for GET requests, otherwise the browser might not propagate the verb and request
            // body correctly.
            if (!String.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpException(403, Strings.SSLRequired);
            }
            else
            {
                // redirect to HTTPS version of page
                string url = "https://" + filterContext.HttpContext.Request.Url.Host + filterContext.HttpContext.Request.RawUrl;
                filterContext.Result = new RedirectResult(url);
            }
        }
    }
}
