using System;
using System.Linq;

namespace NuGetGallery
{
    public interface IModifyCuratedPackageCommand
    {
        void Execute(
            int curatedFeedKey,
            int curatedPackageKey,
            bool included);
    }

    public class ModifyCuratedPackageCommand : AppCommand, IModifyCuratedPackageCommand
    {
        public ModifyCuratedPackageCommand(IEntitiesContext entities)
            : base(entities)
        {
        }

        public void Execute(
            int curatedFeedKey,
            int curatedPackageKey,
            bool included)
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

            curatedPackage.Included = included;

            Entities.SaveChanges();
        }
    }
}