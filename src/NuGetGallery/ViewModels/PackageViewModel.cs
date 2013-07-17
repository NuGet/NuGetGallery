using System;
using System.Linq;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        private readonly Package _package;

        public PackageViewModel(Package package)
        {
            _package = package;
            Version = package.Version;
            Description = package.Description;
            ReleaseNotes = package.ReleaseNotes;
            IconUrl = package.IconUrl;
            ProjectUrl = package.ProjectUrl;
            LicenseUrl = package.LicenseUrl;
            HideLicenseReport = package.HideLicenseReport;
            LatestVersion = package.IsLatest;
            LatestStableVersion = package.IsLatestStable;
            LastUpdated = package.Published;
            Listed = package.Listed;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;

            if (!package.HideLicenseReport)
            {
                LicenseReportUrl = package.LicenseReportUrl;
                
                var licenseNames = package.LicenseNames;
                if (!String.IsNullOrEmpty(licenseNames))
                {
                    LicenseNames = licenseNames.Split(',').Select(l => l.Trim());
                }
            }
            else
            {
                LicenseReportUrl = null;
                LicenseNames = null;
            }
        }

        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string IconUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public Boolean HideLicenseReport { get; set; }
        public IEnumerable<string> LicenseNames { get; set; }
        public string LicenseReportUrl { get; set; }
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
            get { return String.IsNullOrEmpty(_package.Title) ? _package.PackageRegistration.Id : _package.Title; }
        }

        public bool IsCurrent(IPackageVersionModel current)
        {
            return current.Version == Version && current.Id == Id;
        }
    }
}