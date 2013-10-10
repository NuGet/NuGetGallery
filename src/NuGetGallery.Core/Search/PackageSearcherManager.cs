using Lucene.Net.Search;
using System;

namespace NuGetGallery
{
    public class PackageSearcherManager : SearcherManager
    {
        public PackageSearcherManager(Lucene.Net.Store.Directory directory)
            : base(directory)
        {
            WarmTimeStampUtc = DateTime.UtcNow;
        }

        protected override void Warm(IndexSearcher searcher)
        {
            searcher.Search(new MatchAllDocsQuery(), 1);
            WarmTimeStampUtc = DateTime.UtcNow;
        }

        public DateTime WarmTimeStampUtc
        {
            get;
            private set;
        }
    }
}