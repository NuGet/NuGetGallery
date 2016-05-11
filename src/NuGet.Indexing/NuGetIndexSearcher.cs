// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

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
            Filter[][] latest,
            IReadOnlyDictionary<string, int[]> docIdMapping,
            Downloads downloads,
            VersionResult[] versions,
            RankingResult rankings,
            QueryBoostingContext context,
            OpenBitSet latestBitSet,
            OpenBitSet latestStableBitSet,
            OwnersResult owners)
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
            DocIdMapping = docIdMapping;
            Downloads = downloads;
            Versions = versions;
            Rankings = rankings;
            LatestBitSet = latestBitSet;
            LatestStableBitSet = latestStableBitSet;
            Owners = owners;
            QueryBoostingContext = context;
            LastReopen = DateTime.UtcNow;
        }

        public NuGetSearcherManager Manager { get; }
        public IDictionary<string, string> CommitUserData { get; }
        public Downloads Downloads { get; }
        public VersionResult[] Versions { get; }
        public RankingResult Rankings { get; }
        public OpenBitSet LatestBitSet { get; }
        public OpenBitSet LatestStableBitSet { get; }
        public OwnersResult Owners { get; }
        public DateTime LastReopen { get; }
        public IReadOnlyDictionary<string, int[]> DocIdMapping { get; }
        public QueryBoostingContext QueryBoostingContext { get; }

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

        public static int TotalDownloadCounts(VersionResult versions)
        {
            int allVersions = versions.VersionDetails.Select(v => v.Downloads).Sum();

            return allVersions;
        }

        public static Tuple<int, int> DownloadCounts(VersionResult versions, string version)
        {
            int allVersions = TotalDownloadCounts(versions);

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