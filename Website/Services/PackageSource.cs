using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public class PackageSource : IPackageSource
    {
        private readonly IEntityRepository<Package> _packageSet;
        private readonly IEntityRepository<CuratedPackage> _curatedPackageRepository;

        public PackageSource(EntitiesContext entitiesContext)
        {
            _packageSet = new EntityRepository<Package>(entitiesContext);
            _curatedPackageRepository = new EntityRepository<CuratedPackage>(entitiesContext);
        }

        [Ninject.Inject]
        public PackageSource(
            IEntityRepository<Package> packageRepo,
            IEntityRepository<CuratedPackage> curatedPackageRepo)
        {
            _packageSet = packageRepo;
            _curatedPackageRepository = curatedPackageRepo;
        }

        public IQueryable<PackageIndexEntity> GetPackagesForIndexing(DateTime? newerThan)
        {
            IQueryable<Package> set = _packageSet.GetAll()
                .Where(p => p.IsLatest || p.IsLatestStable)  // which implies that p.IsListed by the way!
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            if (newerThan.HasValue)
            {
                // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
                // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                // update independent of the package.
                set = set.Where(p => p.PackageRegistration.Packages.Any(p2 => p2.LastUpdated > newerThan));
            }

            var list = set.ToList();

            var curatedFeedsPerPackageRegistration = _curatedPackageRepository.GetAll()
                .Select(cp => new { cp.PackageRegistrationKey, cp.CuratedFeedKey })
                .GroupBy(x => x.PackageRegistrationKey)
                .ToDictionary(group => group.Key, element => element.Select(x => x.CuratedFeedKey).Distinct());

            Func<int, IEnumerable<int>> GetFeeds = packageRegistrationKey =>
            {
                IEnumerable<int> ret = null;
                curatedFeedsPerPackageRegistration.TryGetValue(packageRegistrationKey, out ret);
                return ret;
            };

            var entities = list.Select(
                p => new PackageIndexEntity 
                {
                    Package = p, CuratedFeedKeys = GetFeeds(p.PackageRegistrationKey)
                });

            return entities.AsQueryable();
        }
    }
}