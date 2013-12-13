using Lucene.Net.Search;
using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageSearcherManager : SearcherManager
    {
        IDictionary<string, IDictionary<string, int>> _currentRankings;
        DateTime _rankingsTimeStampUtc;
        Rankings _rankings;

        public PackageSearcherManager(Lucene.Net.Store.Directory directory, Rankings rankings)
            : base(directory)
        {
            _rankings = rankings;
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

        public IDictionary<string, int> GetRankings(string context)
        {
            if (_currentRankings == null || (DateTime.Now - _rankingsTimeStampUtc) > TimeSpan.FromHours(24))
            {
                _currentRankings = _rankings.Load();
                _rankingsTimeStampUtc = DateTime.UtcNow;
            }

            IDictionary<string, int> rankings;
            if (_currentRankings.TryGetValue(context, out rankings))
            {
                return rankings;
            }

            return _currentRankings["Rank"];
        }
    }
}