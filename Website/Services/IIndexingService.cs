using System;
namespace NuGetGallery
{
    public interface IIndexingService
    {
        DateTime? GetLastWriteTime();
        void UpdateIndex();
        void UpdateIndex(bool forceRefresh);
        void UpdatePackage(Package package);
    }
}