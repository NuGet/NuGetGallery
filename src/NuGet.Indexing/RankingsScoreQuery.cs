// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class RankingScoreQuery : CustomScoreQuery
    {
        const double BaseBoostConstant = 10.0;

        private readonly double _baseBoost;
        private readonly IDictionary<string, int> _rankings;

        public Query Query { get; private set; }

        public RankingScoreQuery(Query query, IDictionary<string, int> rankings, double baseBoost = BaseBoostConstant)
            : base(query)
        {
            Query = query;
            _rankings = rankings;
            _baseBoost = baseBoost;
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
            private readonly IDictionary<string, int> _rankings;
            private readonly string[] _ids;

            public RankingScoreProvider(IndexReader reader, IDictionary<string, int> rankings, double baseBoost)
                : base(reader)
            {
                _rankings = rankings;
                _ids = FieldCache_Fields.DEFAULT.GetStrings(reader, "Id");
                _baseBoost = baseBoost;
            }

            public override float CustomScore(int doc, float subQueryScore, float valSrcScore)
            {
                string id = _ids[doc];

                float score = GetRankingScore(_rankings, id, _baseBoost);
                if (score == 0.0f)
                {
                    return subQueryScore;
                }

                float adjustedScore = subQueryScore * score;
                return adjustedScore;
            }
        }

        public static float GetRankingScore(IDictionary<string, int> rankings, string id, double baseBoost = BaseBoostConstant)
        {
            int ranking = 0;
            if (!rankings.TryGetValue(id, out ranking))
            {
                return 0.0f;
            }

            return (float)Math.Pow(baseBoost, (1.2 - ((double)ranking / ((double)rankings.Count + 1.0))));
        }
    }
}
