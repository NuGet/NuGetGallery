using System.Collections.Generic;

namespace NuGet.Indexing
{
    public abstract class PackageRanking
    {
        public abstract IDictionary<string, IDictionary<string, int>> GetProjectRankings();
        public abstract IDictionary<string, int> GetOverallRanking();
    }
}
