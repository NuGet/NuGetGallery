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
        IDictionary<string, Filter> _curatedFeeds;
        Filter[][] _latest;

        public NuGetIndexSearcher(
            NuGetSearcherManager manager,
            IndexReader reader,
            IndexReader originalReader,
            IDictionary<string, string> commitUserData, 
            IDictionary<string, Filter> curatedFeeds, 
            Filter[][] latest,
            VersionsHandler.VersionResult[] versions,
            IDictionary<string, int> rankings,
            OpenBitSet latestBitSet,
            OpenBitSet latestStableBitSet)
            : base(reader)
        {
            Manager = manager;
            OriginalReader = originalReader;
            CommitUserData = commitUserData;
            _curatedFeeds = curatedFeeds;
            _latest = latest;
            Versions = versions;
            Rankings = rankings;
            LatestBitSet = latestBitSet;
            LatestStableBitSet = latestStableBitSet;
            LastReopen = DateTime.UtcNow;
        }

        public NuGetSearcherManager Manager { get; private set; }
        public IndexReader OriginalReader { get; private set; }
        public IDictionary<string, string> CommitUserData { get; private set; }
        public VersionsHandler.VersionResult[] Versions { get; private set; }
        public IDictionary<string, int> Rankings { get; private set; }
        public OpenBitSet LatestBitSet { get; private set; }
        public OpenBitSet LatestStableBitSet { get; private set; }
        public DateTime LastReopen { get; private set; }

        public Filter GetFilter(bool includeUnlisted, bool includePrerelease, string curatedFeed)
        {
            Filter visibilityFilter = _latest[includeUnlisted ? 1 : 0][includePrerelease ? 1 : 0];

            Filter curatedFeedFilter;
            if (!string.IsNullOrEmpty(curatedFeed) && _curatedFeeds.TryGetValue(curatedFeed, out curatedFeedFilter))
            {
                return new ChainedFilter(new[] { visibilityFilter, curatedFeedFilter }, ChainedFilter.Logic.AND);
            }

            return visibilityFilter;
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
    }
}