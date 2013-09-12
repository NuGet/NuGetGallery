using System.Security.Principal;
using System.Web.Mvc;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        private IIdentity testHookIdentity;

        public virtual IIdentity Identity
        {
            get { return testHookIdentity ?? User.Identity; }
            set { testHookIdentity = value; }
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}