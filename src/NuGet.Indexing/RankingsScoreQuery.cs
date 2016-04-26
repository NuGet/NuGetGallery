// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;

namespace NuGet.Indexing
{
    public class RankingScoreQuery : CustomScoreQuery
    {
        const double BaseBoostConstant = 10.0;

        private readonly double _baseBoost;
        private readonly RankingsHandler.RankingResult _rankings;

        public Query Query { get; private set; }

        public RankingScoreQuery(Query query, RankingsHandler.RankingResult rankings, double baseBoost = BaseBoostConstant)
            : base(query)
        {
            _rankings = rankings;
            _baseBoost = baseBoost;

            Query = query;
        }

        protected override CustomScoreProvider GetCustomScoreProvider(IndexReader reader)
        {
            return new RankingScoreProvider(reader, _rankings, _baseBoost);
        }

        public override string ToString()
        {
            return "rank(" + Query.ToString() + ")";
        }

        private class RankingScoreProvider : CustomScoreProvider
        {
            private readonly double _baseBoost;
            private readonly RankingsHandler.RankingResult _rankings;
            private readonly string _readerName;

            public RankingScoreProvider(IndexReader reader, RankingsHandler.RankingResult rankings, double baseBoost)
                : base(reader)
            {
                _rankings = rankings;
                _baseBoost = baseBoost;

                // We need the reader name: Lucene *may* have multiple segments (which are smaller indices)
                // and RankingsHandler provides us with the per-segment document numbers.
                //
                // If no segments are present (small index) we use an empty string, which is what
                // Lucene also uses internally.
                SegmentReader segmentReader = reader as SegmentReader;

                _readerName = segmentReader != null
                    ? segmentReader.SegmentName
                    : string.Empty;
            }

            public override float CustomScore(int doc, float subQueryScore, float valSrcScore)
            {
                // Check if we want to override the score Lucene calculated. Increasing the score will rank the
                // document higher in search results.
                float score = GetRankingScore(_readerName, _rankings, doc, _baseBoost);
                if (score == 0.0f)
                {
                    return subQueryScore;
                }

                float adjustedScore = subQueryScore * score;
                return adjustedScore * 10;
            }
        }
        
        private static float GetRankingScore(string readerName, RankingsHandler.RankingResult rankings, int doc, double baseBoost = BaseBoostConstant)
        {
            // Override is based on the rankings JSON we generate, containing the top downloaded packages.
            // RankingsHandler has created a mapping for us which has [documentId] = rank,
            // so that we can do a quick lookup based on the document number in the current segment.
            if (rankings.DocumentRankings[readerName].Length < doc)
            {
                return 0.0f;
            }

            var ranking = rankings.DocumentRankings[readerName][doc];
            if (ranking == null)
            {
                return 0.0f;
            }
            
            return (float)Math.Pow(baseBoost, (1.2 - ((double)ranking.Rank / ((double)rankings.Count + 1.0))));
        }
    }
}
