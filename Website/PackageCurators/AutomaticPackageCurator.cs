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

        protected bool DependenciesAreCurated(Package galleryPackage, CuratedFeed curatedFeed)
        {
            Argument.Isset(galleryPackage, "galleryPackage");
            Argument.Isset(curatedFeed, "curatedFeed");
            Argument.Isset(curatedFeed.Packages, "curatedFeed.Packages");

            if (!galleryPackage.Dependencies.AnySafe())
            {
                return true;
            }

            return galleryPackage.Dependencies.All(d => curatedFeed.Packages.Where(p => p.Included).Select(p => p.PackageRegistration.Id).Contains(d.Id));
        }
    }
}