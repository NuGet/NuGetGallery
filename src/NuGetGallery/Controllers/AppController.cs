using System.Security.Principal;
using System.Web.Mvc;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        public virtual IIdentity Identity
        {
            get { return User.Identity; }
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}