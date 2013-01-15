using System;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
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
            IQueryable<Package> collection = _packageSet.GetAll()
                .Where(p => p.IsLatest || p.IsLatestStable)  // which implies that p.IsListed by the way!
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            if (newerThan.HasValue)
            {
                // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
                // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                // update independent of the package.
                return collection.Where(p => p.PackageRegistration.Packages.Any(p2 => p2.LastUpdated > newerThan));
            }

            return collection;
        }
    }
}