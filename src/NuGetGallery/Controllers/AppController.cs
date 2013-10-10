using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        private Lazy<UserSession> _session;
        private IOwinContext _overrideContext;

        [Obsolete("Use UserSession instead!")]
        public virtual IIdentity Identity
        {
            get { return base.User.Identity; }
        }

        [Obsolete("Use UserSession instead!")]
        public new IPrincipal User
        {
            get { return base.User; }
        }

        public UserSession UserSession
        {
            get { return _session.Value; }
        }

        public IOwinContext OwinContext
        {
            get { return _overrideContext ?? HttpContext.GetOwinContext(); }
            set { _overrideContext = value; }
        }

        public AppController()
        {
            _session = new Lazy<UserSession>(LoadSession);
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }

        private UserSession LoadSession()
        {
            if (!HttpContext.Request.IsAuthenticated)
            {
                return null;
            }

            ClaimsPrincipal principal = HttpContext.User as ClaimsPrincipal;
            if (principal == null)
            {
                return null;
            }
            else
            {
                return new UserSession(principal);
            }
        }
    }
}