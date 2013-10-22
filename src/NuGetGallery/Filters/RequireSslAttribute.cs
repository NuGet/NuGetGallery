using System;
using System.Globalization;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Ninject;
using Ninject.Web.Mvc.Filter;
using NuGetGallery.Configuration;

namespace NuGetGallery.Filters
{
    // This code is identical to System.Web.Mvc except that we allow for working in localhost environment without https and we force authenticated users to use SSL
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class RequireSslAttribute : FilterAttribute, IAuthorizationFilter
    {
        private IAppConfiguration _configuration;
        
        public IAppConfiguration Configuration
        {
            get { return _configuration ?? (_configuration = Container.Kernel.Get<IAppConfiguration>()); }
            set { _configuration = value; }
        }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            var request = filterContext.HttpContext.Request;
            if (Configuration.RequireSSL && !request.IsSecureConnection)
            {
                HandleNonHttpsRequest(filterContext);
            }
        }

        private void HandleNonHttpsRequest(AuthorizationContext filterContext)
        {
            // only redirect for GET requests, otherwise the browser might not propagate the verb and request
            // body correctly.
            if (!String.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                filterContext.Result = new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.SSLRequired);
            }
            else
            {
                // redirect to HTTPS version of page
                string portString = String.Empty;
                if (Configuration.SSLPort != 443)
                {
                    portString = String.Format(CultureInfo.InvariantCulture, ":{0}", Configuration.SSLPort);
                }

                string url = "https://" + filterContext.HttpContext.Request.Url.Host + portString + filterContext.HttpContext.Request.RawUrl;
                filterContext.Result = new RedirectResult(url);
            }
        }
    }
}