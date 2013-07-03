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

            if (((package.LicenseReports != null) && (package.LicenseReports.Count() > 0)) 
                && !package.HideLicenseReport)
            {
                var report = package.LicenseReports.OrderByDescending(r => r.CreatedUtc).FirstOrDefault();
                ReportUrl = report.ReportUrl;
                LicensesNames = report.Licenses.Select(p => p.Name);
            }
            else
            {
                HideLicenseReport = true;
                ReportUrl = null;
                LicensesNames = null;
            }
        }

        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string IconUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public Boolean HideLicenseReport { get; set; }
        public IEnumerable<string> LicensesNames { get; set; }
        public string ReportUrl { get; set; }
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