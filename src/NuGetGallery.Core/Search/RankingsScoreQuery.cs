using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class RankingScoreQuery : CustomScoreQuery
    {
        IDictionary<string, int> _rankings;

        public RankingScoreQuery(Query q, IDictionary<string, int> rankings)
            : base(q)
        {
            _rankings = rankings;
        }

        protected override CustomScoreProvider GetCustomScoreProvider(IndexReader reader)
        {
            return new RankingScoreProvider(reader, _rankings);
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

                int ranking = 0;
                _rankings.TryGetValue(id, out ranking);

                const int Range = 200;

                if (ranking == 0 || ranking > Range)
                {
                    return subQueryScore;
                }

                float boost = (float)Math.Pow(10.0, (1.0 - ((double)ranking / ((double)Range + 1.0))));
                float adjustedScore = subQueryScore * boost;
                return adjustedScore;
            }
        }
    }
}
