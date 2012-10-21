namespace NuGetGallery
{
    public class CuratedPackage : IEntity
    {
        public CuratedFeed CuratedFeed { get; set; }
        public int CuratedFeedKey { get; set; }

        public PackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }

        public bool AutomaticallyCurated { get; set; }
        public bool Included { get; set; }
        public string Notes { get; set; }
        public int Key { get; set; }
    }
}