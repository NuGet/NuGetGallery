﻿using System;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        private readonly Package _package;

        public PackageViewModel(Package package)
        {
            _package = package;
            Version = package.Version;
            Description = package.GetCurrentDescription();
            ReleaseNotes = package.ReleaseNotes;
            Title = package.GetCurrentTitle();
            IconUrl = package.GetCurrentIconUrl();
            ProjectUrl = package.GetCurrentProjectUrl();
            LicenseUrl = package.LicenseUrl;
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

        public string Title { get; set; }

        public bool IsCurrent(IPackageVersionModel current)
        {
            return current.Version == Version && current.Id == Id;
        }
    }
}