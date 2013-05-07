using System.Security.Principal;
using System.Web.Mvc;
using NuGetGallery.Commands;

namespace NuGetGallery
{
    public abstract partial class AppController : Controller
    {
        public CommandExecutor Executor { get; protected set; }

        protected virtual IIdentity Identity
        {
            get { return User.Identity; }
        }

        protected AppController(CommandExecutor executor)
        {
            Executor = executor;
        }

        protected virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}