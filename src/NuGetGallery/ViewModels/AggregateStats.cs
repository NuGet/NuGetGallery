using System;

namespace NuGetGallery
{
    public class AggregateStats
    {
        public long Downloads { get; set; }

        public int UniquePackages { get; set; }

        public int TotalPackages { get; set; }

        public DateTime? LastUpdateDateUtc { get; set; }
    }
}