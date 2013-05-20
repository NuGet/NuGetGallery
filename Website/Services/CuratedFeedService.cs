using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class CuratedFeedService : ICuratedFeedService
    {
        protected IEntityRepository<CuratedFeed> CuratedFeedRepository { get; set; }
        protected IEntityRepository<CuratedPackage> CuratedPackageRepository { get; set; }

        protected CuratedFeedService()
        {
        }

        public CuratedFeedService(
            IEntityRepository<CuratedFeed> curatedFeedRepository,
            IEntityRepository<CuratedPackage> curatedPackageRepository)
        {
            CuratedFeedRepository = curatedFeedRepository;
            CuratedPackageRepository = curatedPackageRepository;
        }

        public CuratedPackage CreatedCuratedPackage(
            CuratedFeed curatedFeed,
            PackageRegistration packageRegistration,
            bool included = false,
            bool automaticallyCurated = false,
            string notes = null,
            bool commitChanges = true)
        {
            if (curatedFeed == null)
            {
                throw new ArgumentNullException("curatedFeed");
            }

            if (packageRegistration == null)
            {
                throw new ArgumentNullException("packageRegistration");
            }

            var curatedPackage = curatedFeed.Packages
                .SingleOrDefault(cp => cp.PackageRegistrationKey == packageRegistration.Key);

            if (curatedPackage == null)
            {
                curatedPackage = new CuratedPackage
                {
                    PackageRegistration = packageRegistration,
                    Included = included,
                    AutomaticallyCurated = automaticallyCurated,
                    Notes = notes,
                };

                curatedFeed.Packages.Add(curatedPackage);
            }

            if (commitChanges)
            {
                CuratedFeedRepository.CommitChanges();
            }

            return curatedPackage;
        }

        public void DeleteCuratedPackage(
            int curatedFeedKey,
            int curatedPackageKey)
        {
            var curatedFeed = GetFeedByKey(curatedFeedKey, includePackages: true);
            if (curatedFeed == null)
            {
                throw new InvalidOperationException("The curated feed does not exist.");
            }

            var curatedPackage = curatedFeed.Packages.SingleOrDefault(cp => cp.Key == curatedPackageKey);
            if (curatedPackage == null)
            {
                throw new InvalidOperationException("The curated package does not exist.");
            }

            CuratedPackageRepository.DeleteOnCommit(curatedPackage);
            CuratedPackageRepository.CommitChanges();
        }

        public void ModifyCuratedPackage(
            int curatedFeedKey,
            int curatedPackageKey,
            bool included)
        {
            var curatedFeed = GetFeedByKey(curatedFeedKey, includePackages: true);
            if (curatedFeed == null)
            {
                throw new InvalidOperationException("The curated feed does not exist.");
            }

            var curatedPackage = curatedFeed.Packages.SingleOrDefault(cp => cp.Key == curatedPackageKey);
            if (curatedPackage == null)
            {
                throw new InvalidOperationException("The curated package does not exist.");
            }

            curatedPackage.Included = included;
            CuratedFeedRepository.CommitChanges();
        }

        public CuratedFeed GetFeedByName(string name, bool includePackages)
        {
            IQueryable<CuratedFeed> query = CuratedFeedRepository.GetAll();

            if (includePackages)
            {
                query = query
                    .Include(cf => cf.Packages)
                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration));
            }

            return query
                .SingleOrDefault(cf => cf.Name == name);
        }

        public CuratedFeed GetFeedByKey(int key, bool includePackages)
        {
            IQueryable<CuratedFeed> query = CuratedFeedRepository.GetAll();

            if (includePackages)
            {
                query = query
                    .Include(cf => cf.Packages)
                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration));
            }

            return query
                .SingleOrDefault(cf => cf.Key == key);
        }

        public IEnumerable<CuratedFeed> GetFeedsForManager(int managerKey)
        {
            return CuratedFeedRepository.GetAll()
                .Where(cf => cf.Managers.Any(u => u.Key == managerKey));
        }

        public IQueryable<Package> GetPackages(string curatedFeedName)
        {
            var packages = CuratedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .SelectMany(cf => cf.Packages.SelectMany(cp => cp.PackageRegistration.Packages));

            return packages;
        }

        public IQueryable<PackageRegistration> GetPackageRegistrations(string curatedFeedName)
        {
            var packageRegistrations = CuratedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .SelectMany(cf => cf.Packages.Select(cp => cp.PackageRegistration));

            return packageRegistrations;
        }

        public int? GetKey(string curatedFeedName)
        {
            var results = CuratedFeedRepository.GetAll()
                .Where(cf => cf.Name == curatedFeedName)
                .Select(cf => cf.Key).Take(1).ToArray();

            return results.Length > 0 ? (int?)results[0] : null;
        }
    }
}
