using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    public class IndexDocumentContext
    {
        IDictionary<string, int> _overallRanking;
        IDictionary<string, IDictionary<string, int>> _projectRankings;

        public IndexDocumentContext(IDictionary<string, int> overallRanking, IDictionary<string, IDictionary<string, int>> projectRankings)
        {
            _overallRanking = overallRanking;
            _projectRankings = projectRankings;
        }

        public int GetDocumentRank(string packageId)
        {
            if (_overallRanking != null)
            {
                int val;
                if (_overallRanking.TryGetValue(packageId, out val))
                {
                    return val;
                }
            }
            return 100000;
        }

        public IDictionary<string, int> PivotProjectTypeRanking(string packageId)
        {
            IDictionary<string, int> result = new Dictionary<string, int>();

            if (_projectRankings == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, IDictionary<string, int>> ranking in _projectRankings)
            {
                int rank;
                if (ranking.Value.TryGetValue(packageId, out rank))
                {
                    result.Add(ranking.Key, rank);
                }
            }
            return result;
        }
    }
}
