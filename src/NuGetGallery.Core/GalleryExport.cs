using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;

namespace NuGetGallery
{
    public static class GalleryExport
    {
        public static TextWriter TraceWriter = Console.Out;

        public static List<Tuple<Package, IEnumerable<string>>> GetPackagesSince(string sqlConnectionString, DateTime indexTime, DateTime lastIndexTime)
        {
            return GetPackages(sqlConnectionString, indexTime, lastIndexTime, null);
        }

        public static List<Tuple<Package, IEnumerable<string>>> GetEditedPackagesSince(string sqlConnectionString, DateTime indexTime, DateTime lastIndexTime)
        {
            return GetPackages(sqlConnectionString, indexTime, null, lastIndexTime);
        }

        public static List<Tuple<Package, IEnumerable<string>>> GetAllPackages(string sqlConnectionString, DateTime indexTime)
        {
            return GetPackages(sqlConnectionString, indexTime, null, null);
        }

        public static List<Tuple<Package, IEnumerable<string>>> GetPackages(string sqlConnectionString, DateTime indexTime, DateTime? published, DateTime? lastEdited)
        {
            EntitiesContext context = new EntitiesContext(sqlConnectionString, readOnly: true);
            IEntityRepository<Package> packageSource = new EntityRepository<Package>(context);
            IEntityRepository<CuratedPackage> curatedPackageSource = new EntityRepository<CuratedPackage>(context);

            IQueryable<Package> set = packageSource.GetAll();

            set = set
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Include(p => p.SupportedFrameworks);

            //  always set an upper bound on the set we get as this is what we will timestamp the index with
            set = set.Where(p => p.LastUpdated < indexTime);

            if (published != null)
            {
                //TODO: should this be lastUpdated...
                set = set.Where(p => p.Published >= published);
            }

            else if (lastEdited != null)
            {
                set = set.Where(p => p.LastEdited >= lastEdited);
            }

            TraceWriter.WriteLine(EntityFrameworkTracing.ToTraceString(set));

            //  database call

            DateTime before = DateTime.Now;

            List<Package> list = set.ToList();

            TraceWriter.WriteLine("Packages: {0} rows returned, duration {1} seconds", list.Count, (DateTime.Now - before).TotalSeconds);

            var curatedFeedsPerPackageRegistrationGrouping = curatedPackageSource.GetAll()
                .Include(c => c.CuratedFeed)
                .Select(cp => new { PackageRegistrationKey = cp.PackageRegistrationKey, FeedName = cp.CuratedFeed.Name })
                .GroupBy(x => x.PackageRegistrationKey);

            TraceWriter.WriteLine(EntityFrameworkTracing.ToTraceString(curatedFeedsPerPackageRegistrationGrouping));

            //  database call

            before = DateTime.Now;

            IDictionary<int, IEnumerable<string>> curatedFeedsPerPackageRegistration = curatedFeedsPerPackageRegistrationGrouping
                .ToDictionary(group => group.Key, element => element.Select(x => x.FeedName));

            TraceWriter.WriteLine("Feeds: {0} rows returned, duration {1} seconds", curatedFeedsPerPackageRegistration.Count, (DateTime.Now - before).TotalSeconds);

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
