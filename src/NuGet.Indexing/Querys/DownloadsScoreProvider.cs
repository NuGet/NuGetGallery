// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search.Function;

namespace NuGet.Indexing
{
    public class DownloadsScoreProvider : CustomScoreProvider
    {
        private readonly Downloads _downloads;
        private readonly RankingResult _ranking;
        private readonly IReadOnlyDictionary<string, int[]> _idMapping;
        private readonly QueryBoostingContext _context;

        private readonly double _baseBoost;

        private readonly string _readerName;

        public DownloadsScoreProvider(IndexReader reader,
            IReadOnlyDictionary<string, int[]> idMapping,
            Downloads downloads,
            RankingResult ranking,
            QueryBoostingContext context,
            double baseBoost)
            : base(reader)
        {
            _idMapping = idMapping;
            _downloads = downloads;
            _baseBoost = baseBoost;
            _ranking = ranking;
            _context = context;

            // We need the reader name: Lucene *may* have multiple segments (which are smaller indices)
            // and RankingsHandler provides us with the per-segment document numbers.
            //
            // If no segments are present (small index) we use an empty string, which is what
            // Lucene also uses internally.
            var segmentReader = reader as SegmentReader;

            _readerName = segmentReader != null
                ? segmentReader.SegmentName
                : string.Empty;
        }

        public override float CustomScore(int docId, float subQueryScore, float valSrcScore)
        {
            var adjustedScore = subQueryScore;

            // Check if we want to override the score Lucene calculated.
            // Increasing the score will rank the
            // document higher in search results.
            var rankingAdjuster = RankingScore(_readerName, _ranking, docId, _baseBoost);
            adjustedScore *= rankingAdjuster;

            // If ranking applies, the factor is going to be much bigger than download boost, so stick
            // with it and be done.
            if (rankingAdjuster == 1.0d)
            {
                float downloadAdjuster = AdjustByDownloads(docId);
                adjustedScore *= downloadAdjuster;
            }

            return adjustedScore;
        }

        private float AdjustByDownloads(int docId)
        {
            var indexId = _idMapping[_readerName][docId];

            // Package might not exist in the package table hence the ?.Total
            var totalDownloads = _downloads[indexId]?.Total ?? 0;

            float downloadAdjuster = DownloadScore(totalDownloads, _context);

            return downloadAdjuster;
        }

        public static float DownloadScore(long totalDownloads,
            QueryBoostingContext context)
        {
            if (!context.BoostByDownloads)
            {
                return 1.0f;
            }

            // Logarithmic scale for downloads counts, high downloads get slightly better result up to ~2
            // Packages below the threshold return 1.0
            long adjustedCount = Math.Max(1, totalDownloads - context.Threshold);
            double scoreAdjuster = Math.Log10(adjustedCount) / context.Factor + 1;

            return (float)scoreAdjuster;
        }

        private static float RankingScore(string readerName,
            RankingResult rankings,
            int doc,
            double baseBoost)
        {
            // Override is based on the rankings JSON we generate, containing the top downloaded packages.
            // RankingsHandler has created a mapping for us which has [documentId] = rank,
            // so that we can do a quick lookup based on the document number in the current segment.
            if (rankings.DocumentRankings[readerName].Length < doc)
            {
                return 1.0f;
            }

            var ranking = rankings.DocumentRankings[readerName][doc];
            if (ranking == null)
            {
                return 1.0f;
            }

            var scoreAdjuser = Math.Pow(baseBoost, (1.2 - ((double)ranking.Rank / ((double)rankings.Count + 1.0)))) * 10;

            return (float)scoreAdjuser;
        }
    }
}
