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
            CuratedFeed curatedFeed, 
            PackageRegistration packageRegistration, 
            bool included = false, 
            bool automaticallyCurated = false,
            string notes = null,
            bool commitChanges = true)
        {
            if (curatedFeed == null)
            {
                throw new ArgumentNullException("curatedFeed");
            }

            if (packageRegistration == null)
            {
                throw new ArgumentNullException("packageRegistration");
            }

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