using System.Web.Mvc;
using NuGet;

namespace NuGetGallery
{
    public abstract class AutomaticPackageCurator : IAutomaticPackageCurator
    {
        public abstract void Curate(
            Package galleryPackage,
            IPackage nugetPackage);

        protected virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }
    }
}