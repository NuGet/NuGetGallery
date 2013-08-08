using NuGet;
using NuGetGallery.Core.Packaging;

namespace NuGetGallery
{
    public interface IAutomaticPackageCurator
    {
        void Curate(Package galleryPackage, INupkg nugetPackage, bool commitChanges);
    }
}