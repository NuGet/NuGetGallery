namespace NuGetGallery
{
    public class CuratedPackage : IEntity
    {
        public int Key { get; set; }

        public CuratedFeed CuratedFeed { get; set; }
        public int CuratedFeedKey { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        public string Notes { get; set; }
    }
}