using NuGet;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        void Curate(Package galleryPackage, INupkg nugetPackage, bool commitChanges);
    }
}