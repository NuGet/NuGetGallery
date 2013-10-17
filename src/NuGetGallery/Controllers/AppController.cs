using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        private Lazy<ResolvedUserIdentity> _id;
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

        public IOwinContext OwinContext
        {
            get { return _overrideContext ?? HttpContext.GetOwinContext(); }
            set { _overrideContext = value; }
        }

        public AppController()
        {
            _session = new Lazy<ResolvedUserIdentity>(ResolveUser);
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }

        private ResolvedUserIdentity ResolveUser()
        {
            if (OwinContext.Authentication.User == null)
            {
                return null;
            }

            ClaimsPrincipal principal = OwinContext.Authentication.User as ClaimsPrincipal;
            if (principal == null)
            {
                return null;
            }
            else
            {
                var id = principal.Identities.OfType<ResolvedUserIdentity>().SingleOrDefault();
                if (id != null)
                {
                    return id;
                }
                else
                {
                    id = LoadUser();
                    principal.AddIdentity(id);
                }
            }
        }

        private ResolvedUserIdentity LoadUser()
        {
            throw new NotImplementedException();
        }
    }
}