using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IPackageCache
    {
        void AddPackage(NuGetGallery.Package package);
        IQueryable<string> GetPackageIds(bool includePrerelease = false);
    }

    // This cache is simply used for tab completion data for now, but the goal
    // is to serve the feed and entire site from cache, so this class will
    // get much more complicated.
    public class PackageCache : IPackageCache
    {
        const string _allPackageIdsCacheKey = "all-package-ids";
        const string _releasedPackageIdsCacheKey = "released-package-ids";

        public PackageCache(ICache cache)
        {
            Cache = cache;
        }

        protected ICache Cache { get; set; }

        public void AddPackage(Package package)
        {
            // For now, this is just used to invalidate the cache.
            // But, as more stuff is moved to the cache, this repo
            // will grow to be much more sophisticated.
            var taskFactory = GetService<ITaskFactory>();
            Cache.Remove(_releasedPackageIdsCacheKey);
            taskFactory.StartNew(() => ReadAndCachePackageIds(includePrerelease: false)).LogExceptions();
            Cache.Remove(_allPackageIdsCacheKey);
            taskFactory.StartNew(() => ReadAndCachePackageIds(includePrerelease: true)).LogExceptions();
        }

        protected virtual void CachePackageIds(
            string[] packageIds,
            bool includesPrerelease = false)
        {
            var cacheKey = GetPackageIdsCacheKey(includesPrerelease);
            Cache.Set(cacheKey, packageIds);
        }

        public IQueryable<string> GetPackageIds(bool includePrerelease = false)
        {
            var cacheKey = GetPackageIdsCacheKey(includePrerelease);
            var packageIds = Cache.Get<string[]>(cacheKey);
            if (packageIds == null)
            {
                packageIds = ReadPackageIds(includePrerelease);
                GetService<ITaskFactory>().StartNew(() => CachePackageIds(packageIds, includePrerelease)).LogExceptions();
            }
            return packageIds.AsQueryable();
        }

        static string GetPackageIdsCacheKey(bool includePrerelease = false)
        {
            if (!includePrerelease)
                return _releasedPackageIdsCacheKey;

            return _allPackageIdsCacheKey;
        }

        protected virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }

        string[] ReadPackageIds(bool includePrerelease = false)
        {
            return GetService<IPackageIdsQuery>().Execute(includePrerelease).ToArray();
        }

        protected virtual string[] ReadAndCachePackageIds(bool includePrerelease = false)
        {
            var packageIds = ReadPackageIds(includePrerelease);
            CachePackageIds(packageIds, includePrerelease);
            return packageIds;
        }
    }
}