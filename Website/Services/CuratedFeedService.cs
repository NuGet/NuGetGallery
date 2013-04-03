using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class CuratedFeedService : ICuratedFeedService
    {
        private IEntityRepository<CuratedFeed> _curatedFeedRepository;

        public CuratedFeedService(IEntityRepository<CuratedFeed> curatedFeedRepository)
        {
            _curatedFeedRepository = curatedFeedRepository;
        }

        public IQueryable<Package> GetPackages(string curatedFeedName)
        {
            var packages = _curatedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .SelectMany(cf => cf.Packages.SelectMany(cp => cp.PackageRegistration.Packages));

            return packages;
        }

        public IQueryable<PackageRegistration> GetPackageRegistrations(string curatedFeedName)
        {
            var packageRegistrations = _curatedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .SelectMany(cf => cf.Packages.Select(cp => cp.PackageRegistration));

            return packageRegistrations;
        }

        public int? GetKey(string curatedFeedName)
        {
            var results = _curatedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .Select(cf => cf.Key).Take(1).ToArray();

            return results.Length > 0 ? (int?)results[0] : null;
        }
    }
}