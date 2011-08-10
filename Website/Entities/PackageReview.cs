
namespace NuGetGallery {
    public class PackageReview : IEntity {
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        public string Review { get; set; }
        public int Rating { get; set; }
    }
}