using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class RankingScoreQuery : CustomScoreQuery
    {
        IDictionary<string, int> _rankings;
        public Query Query { get; private set; }

        public RankingScoreQuery(Query q, IDictionary<string, int> rankings)
            : base(q)
        {
            Query = q;
            _rankings = rankings;
        }

        protected override CustomScoreProvider GetCustomScoreProvider(IndexReader reader)
        {
            return new RankingScoreProvider(reader, _rankings);
        }

        public override string ToString()
        {
            return "rank(" + Query.ToString() + ")";
        }

        private class RankingScoreProvider : CustomScoreProvider
        {
            IDictionary<string, int> _rankings;
            string[] _ids;

            public RankingScoreProvider(IndexReader reader, IDictionary<string, int> rankings)
                : base(reader)
            {
                _rankings = rankings;
                _ids = FieldCache_Fields.DEFAULT.GetStrings(reader, "Id");
            }

            public override float CustomScore(int doc, float subQueryScore, float valSrcScore)
            {
                string id = _ids[doc];

                float score = GetRankingScore(_rankings, id);
                if(score == 0.0f) {
                    return subQueryScore;
                }

                float adjustedScore = subQueryScore * score;
                return adjustedScore;
            }
        }

        public static float GetRankingScore(IDictionary<string, int> rankings, string id)
        {
            int ranking = 0;
            if(!rankings.TryGetValue(id, out ranking)) {
                return 0.0f;
            }
            return (float)Math.Pow(10.0, (1.1 - ((double)ranking / ((double)rankings.Count + 1.0))));
        }
    }
}
