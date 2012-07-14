using System;
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

        public bool IsLatest { get; set; }

        public bool IsLatestStable { get; set; }

        public DateTime Published { get; set; }
    }
}