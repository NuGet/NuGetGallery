using System;
using System.Linq;

namespace NuGetGallery
{
    public interface IDeleteCuratedPackageCommand
    {
        void Execute(
            int curatedFeedKey,
            int curatedPackageKey);
    }

    public class DeleteCuratedPackageCommand : AppCommand, IDeleteCuratedPackageCommand
    {
        public DeleteCuratedPackageCommand(IEntitiesContext entities)
            : base(entities)
        {
        }

        public void Execute(
            int curatedFeedKey,
            int curatedPackageKey)
        {
            var curatedFeed = GetService<ICuratedFeedService>().GetFeedByKey(curatedFeedKey, includePackages: true);
            if (curatedFeed == null)
            {
                throw new InvalidOperationException("The curated feed does not exist.");
            }

            var curatedPackage = curatedFeed.Packages.SingleOrDefault(cp => cp.Key == curatedPackageKey);
            if (curatedPackage == null)
            {
                throw new InvalidOperationException("The curated package does not exist.");
            }

            Entities.CuratedPackages.Remove(curatedPackage);

            Entities.SaveChanges();
        }
    }
}