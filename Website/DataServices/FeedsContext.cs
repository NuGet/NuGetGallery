using System.Linq;

namespace NuGetGallery {
    public class FeedsContext {
        private IEntityRepository<Package> packageRepo;

        public FeedsContext(IEntityRepository<Package> packageRepo) {
            this.packageRepo = packageRepo;
        }
        
        public IQueryable<FeedPackage> Packages {
            get {
                return packageRepo.GetAll().ToFeedPackageQuery();
            }
        }
    }
}
