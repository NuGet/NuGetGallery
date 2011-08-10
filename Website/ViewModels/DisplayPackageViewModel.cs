
using System.Collections.Generic;
namespace NuGetGallery {
    public class DisplayPackageViewModel {
        public DisplayPackageViewModel(
            string id,
            string version) {
            Id = id;
            Version = version;
        }

        public string Id { get; set; }
        public string Version { get; set; }

        public IEnumerable<string> Authors { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }

        public bool LatestVersion { get; set; }
        public bool Prerelease { get; set; }

        public int DownloadCount { get; set; }
        public int RatingCount { get; set; }
        public float RatingAverage { get; set; }
    }
}