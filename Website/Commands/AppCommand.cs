using System.Web.Mvc;

namespace NuGetGallery
{
    public abstract class AppCommand
    {
        protected AppCommand(IEntitiesContext entities)
        {
            Entities = entities;
        }

        protected IEntitiesContext Entities { get; set; }

        protected virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}