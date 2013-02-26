using NuGet;

namespace NuGetGallery
{
    public interface IAutomaticallyCuratePackageCommand
    {
        void Execute(
            Package galleryPackage,
            IPackage nugetPackage,
            bool commitChanges);
    }

    public class AutomaticallyCuratePackageCommand : AppCommand, IAutomaticallyCuratePackageCommand
    {
        public AutomaticallyCuratePackageCommand(IEntitiesContext entities)
            : base(entities)
        {
        }

        public void Execute(Package galleryPackage, IPackage nugetPackage, bool commitChanges)
        {
            foreach (var curator in GetServices<IAutomaticPackageCurator>())
            {
                curator.Curate(galleryPackage, nugetPackage, commitChanges: commitChanges);
            }
        }
    }
}