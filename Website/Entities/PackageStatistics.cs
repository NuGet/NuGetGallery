using System;

namespace NuGetGallery
{
    public class PackageStatistics : IEntity
    {
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        public DateTime Timestamp { get; set; }
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
    }
}