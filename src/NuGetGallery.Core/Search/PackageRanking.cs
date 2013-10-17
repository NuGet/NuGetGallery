using System.Collections.Generic;

namespace NuGetGallery
{
    public abstract class PackageRanking
    {
        public abstract IDictionary<string, IDictionary<string, int>> GetProjectRankings();
        public abstract IDictionary<string, int> GetOverallRanking();
    }
}
