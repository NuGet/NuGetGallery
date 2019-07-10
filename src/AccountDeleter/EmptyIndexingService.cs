using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    public class EmptyIndexingService : IIndexingService
    {
        public EmptyIndexingService()
        {

        }

        public string IndexPath => throw new NotImplementedException();

        public bool IsLocal => throw new NotImplementedException();

        public Task<int> GetDocumentCount()
        {
            throw new NotImplementedException();
        }

        public Task<long> GetIndexSizeInBytes()
        {
            throw new NotImplementedException();
        }

        public Task<DateTime?> GetLastWriteTime()
        {
            throw new NotImplementedException();
        }

        public void UpdateIndex()
        {
            // Do nothing. We have no index to update here.
        }

        public void UpdateIndex(bool forceRefresh)
        {
            UpdateIndex();
        }

        public void UpdatePackage(Package package)
        {
            UpdateIndex();
        }
    }
}
