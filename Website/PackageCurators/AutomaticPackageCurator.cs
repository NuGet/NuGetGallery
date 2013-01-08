using System.Linq;
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

        protected static bool DependenciesAreCurated(Package galleryPackage, CuratedFeed curatedFeed)
        {
            if (galleryPackage != null && galleryPackage.Dependencies.AnySafe())
            {
                return galleryPackage.Dependencies.All(d => curatedFeed.Packages.Where(p => p.Included).Select(p => p.PackageRegistration.Id).Contains(d.Id));
            }

            return true;
        }
    }
}