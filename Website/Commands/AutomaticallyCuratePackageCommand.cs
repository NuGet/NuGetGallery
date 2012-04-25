using NuGet;

namespace NuGetGallery
{
    public interface IAutomaticallyCuratePackageCommand
    {
        void Execute(
            Package galleryPackage,
            IPackage nugetPackage);
    }

    public class AutomaticallyCuratePackageCommand : AppCommand, IAutomaticallyCuratePackageCommand
    {
        public AutomaticallyCuratePackageCommand(IEntitiesContext entities)
            : base(entities)
        {
        }

        public void Execute(
            Package galleryPackage,
            IPackage nugetPackage)
        {
            foreach(var curator in GetServices<IAutomaticPackageCurator>())
            {
                curator.Curate(
                    galleryPackage,
                    nugetPackage);
            }
        }
    }
}