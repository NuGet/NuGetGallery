using NuGet;
using NuGetGallery.Data.Model;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        void Curate(Package galleryPackage, INupkg nugetPackage, bool commitChanges);
    }
}