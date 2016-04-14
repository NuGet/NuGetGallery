// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public class NuGetIndexSearcher : IndexSearcher
    {
        private readonly IDictionary<string, Filter> _curatedFeeds;
        private readonly Filter[][] _latest;

        public NuGetIndexSearcher(
            NuGetSearcherManager manager, 
            IndexReader reader,
            IDictionary<string, string> commitUserData, 
            IDictionary<string, Filter> curatedFeeds, 
            Filter[][] latest, VersionsHandler.VersionResult[] versions, 
            RankingsHandler.RankingResult rankings, 
            OpenBitSet latestBitSet, 
            OpenBitSet latestStableBitSet,
            OwnersHandler.OwnersResult owners)
            : base(reader)
        {
            Manager = manager;
            CommitUserData = commitUserData;

            _curatedFeeds = new Dictionary<string, Filter>(curatedFeeds.Count);
            foreach (var curatedFeedsFilter in curatedFeeds)
            {
                _curatedFeeds.Add(curatedFeedsFilter.Key, new CachingWrapperFilter(curatedFeedsFilter.Value));
            }
            
            _latest = latest;
            Versions = versions;
            Rankings = rankings;
            LatestBitSet = latestBitSet;
            LatestStableBitSet = latestStableBitSet;
            Owners = owners;
            LastReopen = DateTime.UtcNow;
        }

        public NuGetSearcherManager Manager { get; private set; }
        public IDictionary<string, string> CommitUserData { get; private set; }
        public VersionsHandler.VersionResult[] Versions { get; private set; }
        public RankingsHandler.RankingResult Rankings { get; private set; }
        public OpenBitSet LatestBitSet { get; private set; }
        public OpenBitSet LatestStableBitSet { get; private set; }
        public OwnersHandler.OwnersResult Owners { get; private set; }
        public DateTime LastReopen { get; private set; }

        public bool TryGetFilter(string curatedFeed, out Filter filter)
        {
            filter = null;

            if (!string.IsNullOrEmpty(curatedFeed) && _curatedFeeds.TryGetValue(curatedFeed, out filter))
            {
                return true;
            }

            return false;
        }

        public bool TryGetFilter(bool includeUnlisted, bool includePrerelease, string curatedFeed, out Filter filter)
        {
            Filter visibilityFilter = _latest[includeUnlisted ? 1 : 0][includePrerelease ? 1 : 0];

            Filter curatedFeedFilter;
            if (!string.IsNullOrEmpty(curatedFeed) && _curatedFeeds.TryGetValue(curatedFeed, out curatedFeedFilter))
            {
                filter = new ChainedFilter(new[] { visibilityFilter, curatedFeedFilter }, ChainedFilter.Logic.AND);
                return true;
            }

            filter = visibilityFilter;
            return true;
        }

        public static Tuple<int, int> GetDownloadCounts(VersionsHandler.VersionResult versions, string version)
        {
            int allVersions = versions.VersionDetails.Select(v => v.Downloads).Sum();

            int thisVersion = versions.VersionDetails
                .Where(v => v.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
                .Select(v => v.Downloads)
                .FirstOrDefault();

            return Tuple.Create(allVersions, thisVersion);
        }

        public static IEnumerable<string> GetOwners(NuGetIndexSearcher searcher, string id)
        {
            HashSet<string> owners;
            if (searcher.Owners.PackagesWithOwners.TryGetValue(id, out owners))
            {
                return owners;
            }

            return Enumerable.Empty<string>();
        }
    }
}