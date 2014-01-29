using System;
using System.Security.Claims;
using System.Security.Principal;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using Ninject;
using NuGetGallery.Authentication;
using System.Net;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        private IOwinContext _overrideContext;

        public IOwinContext OwinContext
        {
            get { return _overrideContext ?? HttpContext.GetOwinContext(); }
            set { _overrideContext = value; }
        }

        public NuGetContext NuGetContext { get; private set; }

        public new ClaimsPrincipal User
        {
            get { return base.User as ClaimsPrincipal; }
        }

        protected AppController()
        {
            NuGetContext = new NuGetContext(this);
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }

        protected internal User GetCurrentUser()
        {
            return OwinContext.GetCurrentUser();
        }

        protected internal virtual ActionResult SafeRedirect(string returnUrl)
        {
            return new SafeRedirectResult(returnUrl, Url.Home());
        }
    }

    public class NuGetContext
    {
        private Lazy<User> _currentUser;
        
        public ConfigurationService Config { get; internal set; }
        public User CurrentUser { get { return _currentUser.Value; } }

        public NuGetContext(AppController ctrl)
        {
            Config = Container.Kernel.TryGet<ConfigurationService>();

            _currentUser = new Lazy<User>(() =>
                ctrl.OwinContext.GetCurrentUser());
        }
    }
}