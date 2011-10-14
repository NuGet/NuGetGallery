using System.Linq;

namespace NuGetGallery
{
    public class V2Feed : FeedServiceBase<V2FeedPackage>
    {
        protected override FeedContext<V2FeedPackage> CreateDataSource()
        {
            return new FeedContext<V2FeedPackage> { Packages = PackageRepo.GetAll().ToV2FeedPackageQuery() };
        }

        public override IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework)
        {
            // Filter out unlisted packages when searching. We will return it when a generic "GetPackages" request comes and filter it on the client.
            return PackageRepo.GetAll()
                              .Where(p => p.Listed)
                              .Search(searchTerm)
                              .ToV2FeedPackageQuery();
        }
    }
}
