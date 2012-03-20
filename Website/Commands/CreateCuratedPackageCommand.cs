using System;

namespace NuGetGallery
{
    public interface ICreateCuratedPackageCommand
    {
        CuratedPackage Execute(
            int curatedFeedKey, 
            int packageRegistrationKey,
            bool included = true,
            bool automaticallyCurated = false,
            string notes = null);
    }

    public class CreateCuratedPackageCommand : AppCommand, ICreateCuratedPackageCommand
    {
        public CreateCuratedPackageCommand(IEntitiesContext entities)
            : base(entities)
        {
        }

        public CuratedPackage Execute(
            int curatedFeedKey, 
            int packageRegistrationKey,
            bool included = true,
            bool automaticallyCurated = false,
            string notes = null)
        {
            var curatedFeed = GetService<ICuratedFeedByKeyQuery>().Execute(curatedFeedKey);
            if (curatedFeed == null)
                throw new InvalidOperationException("The curated feed does not exist.");

            var packageRegistration = GetService<IPackageRegistrationByKeyQuery>().Execute(packageRegistrationKey);
            if (packageRegistration == null)
                throw new InvalidOperationException("The package ID to curate does not exist.");

            var curatedPackage = new CuratedPackage
            {
                PackageRegistrationKey = packageRegistration.Key,
                Included = included,
                AutomaticallyCurated = automaticallyCurated,
                Notes = notes,
            };

            curatedFeed.Packages.Add(curatedPackage);

            Entities.SaveChanges();

            return curatedPackage;
        }
    }
}