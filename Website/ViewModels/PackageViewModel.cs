using System;
namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        readonly Package package;

        public PackageViewModel(Package package)
        {
            this.package = package;
            Version = package.Version;
            Description = package.Description;
            ReleaseNotes = package.ReleaseNotes;
            IconUrl = package.IconUrl;
            ProjectUrl = package.ProjectUrl;
            LicenseUrl = package.LicenseUrl;
            LatestVersion = package.IsLatest;
            LatestStableVersion = package.IsLatestStable;
            LastUpdated = package.LastUpdated;
            Listed = package.Listed;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;
        }

        public string Id
        {
            get
            {
                return package.PackageRegistration.Id;
            }
        }
        public string Version { get; set; }
        public string Title
        {
            get
            {
                return String.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;
            }
        }
        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string IconUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool LatestVersion { get; set; }
        public bool LatestStableVersion { get; set; }
        public bool Prerelease { get; set; }
        public int DownloadCount { get; set; }
        public bool Listed { get; set; }
        public int TotalDownloadCount
        {
            get
            {
                return package.PackageRegistration.DownloadCount;
            }
        }

        public bool IsCurrent(IPackageVersionModel current)
        {
            return current.Version == Version && current.Id == Id;
        }
    }
}