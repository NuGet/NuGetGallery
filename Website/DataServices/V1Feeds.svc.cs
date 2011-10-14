using System.Linq;

namespace NuGetGallery
{
    public class V1Feed : FeedServiceBase<V1FeedPackage>
    {
        protected override FeedContext<V1FeedPackage> CreateDataSource()
        {
            return new FeedContext<V1FeedPackage>
            {
                Packages = PackageRepo.GetAll()
                                      .Where(p => !p.IsPrerelease)
                                      .ToV1FeedPackageQuery()
            };
        }

        public override IQueryable<V1FeedPackage> Search(string searchTerm, string targetFramework)
        {
            // Only allow listed stable releases to be returned when searching the v1 feed.
            return PackageRepo.GetAll()
                              .Where(p => !p.IsPrerelease && p.Listed)
                              .Search(searchTerm)
                              .ToV1FeedPackageQuery();
        }
    }
}
