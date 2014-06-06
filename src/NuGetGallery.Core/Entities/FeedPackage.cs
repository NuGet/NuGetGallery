using System;
using System.ComponentModel.DataAnnotations;
namespace NuGetGallery
{
    public class FeedPackage : IEntity
    {
        public Feed Feed { get; set; }
        public int FeedKey { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }

        [Required]
        public DateTime Added { get; set; }

        public int Key { get; set; }
    }
}
