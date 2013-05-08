using Microsoft.Internal.Web.Utils;
namespace NuGetGallery
{
    public class AggregateStats
    {
        public long Downloads { get; set; }

        public int UniquePackages { get; set; }

        public int TotalPackages { get; set; }

        // Properly implemented equality makes tests easier!
        public override bool Equals(object obj)
        {
            AggregateStats other = obj as AggregateStats;
            return other != null && 
                   other.Downloads == Downloads && 
                   other.UniquePackages == UniquePackages && 
                   other.TotalPackages == TotalPackages;
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(Downloads)
                .Add(UniquePackages)
                .Add(TotalPackages)
                .CombinedHash;
        }
    }
}