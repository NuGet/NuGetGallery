using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Infrastructure.Lucene
{
    public class ExternalSearchService : ISearchService, IIndexingService
    {
        public Uri ServiceUri { get; private set; }

        public IQueryable<Package> Search(SearchFilter filter, out int totalHits)
        {
            throw new NotImplementedException();
        }

        public DateTime? GetLastWriteTime()
        {
            
        }

        public void UpdateIndex()
        {
            // No-op
        }

        public void UpdateIndex(bool forceRefresh)
        {
            // No-op
        }

        public void UpdatePackage(Package package)
        {
            // No-op
        }

        public int GetDocumentCount()
        {
            // No-op
        }

        public long GetIndexSizeInBytes()
        {
            
        }

        public string IndexPath
        {
            get { return ServiceUri.AbsoluteUri; }
        }
    }
}