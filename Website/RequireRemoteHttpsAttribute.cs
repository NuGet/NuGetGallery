using System;
using System.Globalization;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Ninject;
using Ninject.Web.Mvc.Filter;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    // This code is identical to System.Web.Mvc except that we allow for working in localhost environment without https and we force authenticated users to use SSL
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class RequireRemoteHttpsAttribute : FilterAttribute, IAuthorizationFilter
    {
        private IAppConfiguration _configuration;
        private IFormsAuthenticationService _formsAuth;

        public IAppConfiguration Configuration
        {
            get { return _configuration ?? (_configuration = Container.Kernel.Get<IAppConfiguration>()); }
            set { _configuration = value; }
        }

        public IFormsAuthenticationService FormsAuthentication
        {
            get { return _formsAuth ?? (_formsAuth = Container.Kernel.Get<IFormsAuthenticationService>()); }
            set { _formsAuth = value; }
        }

        public bool OnlyWhenAuthenticated { get; set; }

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            var request = filterContext.HttpContext.Request;
            if (Configuration.RequireSSL && !request.IsSecureConnection && ShouldForceSSL(filterContext.HttpContext))
            {
                HandleNonHttpsRequest(filterContext);
            }
        }

        private bool ShouldForceSSL(HttpContextBase context)
        {
            return !OnlyWhenAuthenticated || // If OnlyWhenAuthenticated == false, then we should force SSL
                context.Request.IsAuthenticated || // If Authenticated, force SSL (we should already be on SSL, since the cookie is secure, but just in case...)
                FormsAuthentication.ShouldForceSSL(context); // If the "ForceSSL" cookie is present.
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