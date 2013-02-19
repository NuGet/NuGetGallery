using System;

namespace NuGetGallery
{
    public interface ICreateCuratedPackageCommand
    {
        CuratedPackage Execute(
            CuratedFeed curatedFeed, 
            PackageRegistration packageRegistration, 
            bool included = true, 
            bool automaticallyCurated = false, 
            string notes = null,
            bool commitChanges = true);
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
            string notes = null,
            bool commitChanges = true)
        {
            var curatedFeed = GetService<ICuratedFeedByKeyQuery>().Execute(curatedFeedKey, includePackages: false);
            if (curatedFeed == null)
            {
                throw new InvalidOperationException("The curated feed does not exist.");
            }

            var packageRegistration = GetService<IPackageRegistrationByKeyQuery>().Execute(packageRegistrationKey, includeOwners: false);
            if (packageRegistration == null)
            {
                throw new InvalidOperationException("The package ID to curate does not exist.");
            }

            return Execute(
                curatedFeedKey,
                packageRegistrationKey,
                included,
                automaticallyCurated,
                notes,
                commitChanges);
        }

        public CuratedPackage Execute(
            CuratedFeed curatedFeed, 
            PackageRegistration packageRegistration, 
            bool included = false, 
            bool automaticallyCurated = false,
            string notes = null,
            bool commitChanges = true)
        {
            var curatedPackage = new CuratedPackage
            {
                PackageRegistrationKey = packageRegistration.Key,
                Included = included,
                AutomaticallyCurated = automaticallyCurated,
                Notes = notes,
            };

            curatedFeed.Packages.Add(curatedPackage);

            if (commitChanges)
            {
                Entities.SaveChanges();
            }

            return curatedPackage;
        }
    }
}