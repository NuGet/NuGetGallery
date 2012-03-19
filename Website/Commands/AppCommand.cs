using System.Collections.Generic;
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

        protected virtual IEnumerable<T> GetServices<T>()
        {
            return DependencyResolver.Current.GetServices<T>();
        }
    }
}