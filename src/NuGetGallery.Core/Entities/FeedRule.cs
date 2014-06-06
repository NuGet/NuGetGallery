using System.ComponentModel.DataAnnotations;
namespace NuGetGallery
{
    public class FeedRule : IEntity
    {
        public Feed Feed { get; set; }
        public int FeedKey { get; set; }

        public PackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }

        [StringLength(256)]
        public string PackageVersionSpec { get; set; }

        [StringLength(512)]
        public string Notes { get; set; }
        
        public int Key { get; set; }
    }
}
