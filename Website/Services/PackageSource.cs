using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace NuGetGallery.Services
{
    public class PackageSource : IPackageSource
    {
        private readonly IEntityRepository<Package> _packageSet;

        public PackageSource(EntitiesContext entitiesContext)
        {
            _packageSet = new EntityRepository<Package>(entitiesContext);
        }

        [Ninject.Inject]
        public PackageSource(IEntityRepository<Package> packageRepo)
        {
            _packageSet = packageRepo;
        }

        public IQueryable<Package> GetPackagesForIndexing(DateTime? newerThan)
        {
            var collection = _packageSet.GetAll()
                .Where(x => x.IsLatest || x.IsLatestStable); // which implies that x.IsListed by the way!

            if (newerThan.HasValue)
            {
                // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
                // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                // update independent of the package.
                return collection.Where(x => x.PackageRegistration.Packages.Any(p2 => p2.LastUpdated > newerThan));
            }
            else
            {
                return collection;
            }
        }
    }
}