namespace NuGetGallery
{
    public class PackageIndexEntity
    {
        public int Key { get; set; }

        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Tags { get; set; }

        public string Authors { get; set; }

        public int DownloadCount { get; set; }

        public int? LatestKey { get; set; }
    }
}