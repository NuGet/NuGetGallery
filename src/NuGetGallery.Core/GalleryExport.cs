using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public static class GalleryExport
    {
        public static List<Tuple<Package, IEnumerable<string>>> GetPackagesAndFeeds(string sqlConnectionString, bool all, DateTime? lastIndexTime, DateTime? first, DateTime? last)
        {
            EntitiesContext context = new EntitiesContext(sqlConnectionString, readOnly: true);
            IEntityRepository<NuGetGallery.Package> packageSource = new EntityRepository<NuGetGallery.Package>(context);
            IEntityRepository<NuGetGallery.CuratedPackage> curatedPackageSource = new EntityRepository<NuGetGallery.CuratedPackage>(context);

            IQueryable<Package> set = packageSource.GetAll();

            if (!all)
            {
                if (lastIndexTime.HasValue)
                {
                    // Retrieve the Latest and LatestStable version of packages if any package for that registration changed since we last updated the index.
                    // We need to do this because some attributes that we index such as DownloadCount are values in the PackageRegistration table that may
                    // update independent of the package.
                    set = set.Where(
                        p => (p.IsLatest || p.IsLatestStable) &&
                            p.PackageRegistration.Packages.Any(p2 => p2.LastUpdated > lastIndexTime));
                }
                else
                {
                    set = set.Where(p => p.IsLatest || p.IsLatestStable);  // which implies that p.IsListed by the way!
                }
            }

            if (first != null)
            {
                set = set.Where(p => (p.Created >= first));
            }
            if (last != null)
            {
                set = set.Where(p => (p.Created <= last));
            }

            IQueryable<Package> obj = set
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            Console.WriteLine(EntityFrameworkTracing.ToTraceString(obj));

            //  database call

            List<Package> list = obj.ToList();

            var curatedFeedsPerPackageRegistrationGrouping = curatedPackageSource.GetAll()
                .Include(c => c.CuratedFeed)
                .Select(cp => new { PackageRegistrationKey = cp.PackageRegistrationKey, FeedName = cp.CuratedFeed.Name })
                .GroupBy(x => x.PackageRegistrationKey);

            Console.WriteLine(EntityFrameworkTracing.ToTraceString(curatedFeedsPerPackageRegistrationGrouping));

            //  database call

            IDictionary<int, IEnumerable<string>> curatedFeedsPerPackageRegistration = curatedFeedsPerPackageRegistrationGrouping
                .ToDictionary(group => group.Key, element => element.Select(x => x.FeedName));

            Func<int, IEnumerable<string>> GetFeeds = packageRegistrationKey =>
            {
                IEnumerable<string> ret = null;
                curatedFeedsPerPackageRegistration.TryGetValue(packageRegistrationKey, out ret);
                return ret;
            };

            List<Tuple<Package, IEnumerable<string>>> packagesAndFeeds = list
                .Select(p => new Tuple<Package, IEnumerable<string>>(p, GetFeeds(p.PackageRegistrationKey)))
                .ToList();

            return packagesAndFeeds;
        }


    }
}
