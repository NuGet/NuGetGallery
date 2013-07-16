using System;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        private readonly Package _package;

        public PackageViewModel(Package package)
        {
            _package = package;
            Version = package.Version;
            Description = package.Metadata.Description;
            ReleaseNotes = package.Metadata.ReleaseNotes;
            IconUrl = package.Metadata.IconUrl;
            ProjectUrl = package.Metadata.ProjectUrl;
            LicenseUrl = package.Metadata.LicenseUrl;
            LatestVersion = package.IsLatest;
            LatestStableVersion = package.IsLatestStable;
            LastUpdated = package.Published;
            Listed = package.Listed;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;
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
            get { return _package.PackageRegistration.DownloadCount; }
        }

        public string Id
        {
            get { return _package.PackageRegistration.Id; }
        }

        public string Version { get; set; }

        public string Title
        {
            get { return String.IsNullOrEmpty(_package.Metadata.Title) ? _package.PackageRegistration.Id : _package.Metadata.Title; }
        }

        public bool IsCurrent(IPackageVersionModel current)
        {
            return current.Version == Version && current.Id == Id;
        }
    }
}