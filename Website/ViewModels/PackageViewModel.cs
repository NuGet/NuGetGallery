using System;
namespace NuGetGallery {
    public class PackageViewModel : IPackageVersionModel {
        public PackageViewModel(Package package) {
            Id = package.PackageRegistration.Id;
            Version = package.Version;
            Title = String.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;
            Description = package.Description;
            IconUrl = package.IconUrl;
            ProjectUrl = package.ProjectUrl;
            LicenseUrl = package.LicenseUrl;
            LatestVersion = package.IsLatest;
            Prerelease = package.IsPrerelease;
            DownloadCount = package.DownloadCount;
            LastUpdated = package.LastUpdated;
        }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool LatestVersion { get; set; }
        public bool Prerelease { get; set; }
        public int DownloadCount { get; set; }

        public bool IsCurrent(IPackageVersionModel current) {
            return current.Version == Version && current.Id == Id;
        }
    }
}