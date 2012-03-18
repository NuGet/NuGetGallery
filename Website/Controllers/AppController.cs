using System.Security.Principal;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class AppController : Controller
    {
        protected virtual IIdentity Identity
        {
            get { return User.Identity; }
        }

        protected virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}