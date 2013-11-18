using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using System;

namespace NuGetGallery
{
    public class RankingBoostingQuery : CustomScoreQuery
    {
        string _context;

        public RankingBoostingQuery(Query q, string context)
            : base(q)
        {
            _context = context;
        }

        protected override CustomScoreProvider GetCustomScoreProvider(IndexReader reader)
        {
            return new DownloadBooster(reader, _context);
        }

        private class DownloadBooster : CustomScoreProvider
        {
            int[] _rankings;

            public DownloadBooster(IndexReader reader, string context)
                : base(reader)
            {
                _rankings = FieldCache_Fields.DEFAULT.GetInts(reader, context);
            }

            public override float CustomScore(int doc, float subQueryScore, float valSrcScore)
            {
                int ranking = _rankings[doc];
                if (ranking == 0 || ranking > 200)
                {
                    return subQueryScore;
                }
                float boost = (float)Math.Pow(10.0, (1.0 - ((double)ranking / 200.0)));
                float adjustedScore = subQueryScore * boost;
                return adjustedScore;
            }
        }
    }
}
