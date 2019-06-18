using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    public class EmptyIndexingService : IIndexingService
    {
        public EmptyIndexingService() { }

        public string IndexPath => throw new NotImplementedException();

        public bool IsLocal => throw new NotImplementedException();

        public Task<int> GetDocumentCount()
        {
            // This should not be called
            throw new NotImplementedException();
        }

        public Task<long> GetIndexSizeInBytes()
        {
            // This should not be called
            throw new NotImplementedException();
        }

        public Task<DateTime?> GetLastWriteTime()
        {
            // This should not be called
            throw new NotImplementedException();
        }

        public void UpdateIndex()
        {
            // no-op. There is no index to update.
        }

        public void UpdateIndex(bool forceRefresh)
        {
            // no-op. There is no index to update.
        }

        public void UpdatePackage(Package package)
        {
            // no-op. There is no index to update.
        }
    }
}
