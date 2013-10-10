using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;

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
                if (ranking == 0 || ranking > 1000)
                {
                    return subQueryScore;
                }
                float boost = (((1000.0f - ranking) / 100.0f) * 5.0f) + 1.0f;
                float adjustedScore = subQueryScore * boost;
                return adjustedScore;
            }
        }
    }
}
