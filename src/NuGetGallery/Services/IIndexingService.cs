using System;
using NuGetGallery.Configuration;
namespace NuGetGallery
{
    public interface IIndexingService
    {
        DateTime? GetLastWriteTime();
        void UpdateIndex();
        void UpdateIndex(bool forceRefresh);
        void UpdatePackage(Package package);

        int GetDocumentCount();
        long GetIndexSizeInBytes();

        string IndexPath { get; }
    }
}