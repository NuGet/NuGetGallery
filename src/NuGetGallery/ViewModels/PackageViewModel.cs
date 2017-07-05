// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Linq;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        private readonly Package _package;
        private readonly bool _isSemVer2;
        private string _pendingTitle;
        private string _fullVersion;

        public PackageViewModel(Package package)
        {
            _package = package;

            _fullVersion = NuGetVersionFormatter.ToFullStringOrFallback(package.Version, fallback: package.Version);
            _isSemVer2 = package.SemVerLevelKey == SemVerLevelKey.SemVer2;

            Version = String.IsNullOrEmpty(package.NormalizedVersion) ?
                NuGetVersionFormatter.Normalize(package.Version) :
                package.NormalizedVersion;
            
            Description = package.Description;
            ReleaseNotes = package.ReleaseNotes;
            IconUrl = package.IconUrl;
            ProjectUrl = package.ProjectUrl;
            LicenseUrl = package.LicenseUrl;
            HideLicenseReport = package.HideLicenseReport;
            LatestVersion = package.IsLatest;
            LatestVersionSemVer2 = package.IsLatestSemVer2;
            LatestStableVersion = package.IsLatestStable;
            LatestStableVersionSemVer2 = package.IsLatestStableSemVer2;
            LastUpdated = package.Published;
            Listed = package.Listed;
            Deleted = package.Deleted;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;
            LicenseReportUrl = package.LicenseReportUrl;

            var licenseNames = package.LicenseNames;
            if (!String.IsNullOrEmpty(licenseNames))
            {
                LicenseNames = licenseNames.Split(',').Select(l => l.Trim());
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
        public bool LatestVersionSemVer2 { get; set; }
        public bool LatestStableVersionSemVer2 { get; set; }
        public bool Prerelease { get; set; }
        public int DownloadCount { get; set; }
        public bool Listed { get; set; }
        public bool Deleted { get; set; }

        public int TotalDownloadCount
        {
            get { return _package.PackageRegistration.DownloadCount; }
        }

        public string Id
        {
            get { return _package.PackageRegistration.Id; }
        }

        public string Version { get; set; }
        public string FullVersion => _fullVersion;
        public bool IsSemVer2 => _isSemVer2;

        public string Title
        {
            get { return _pendingTitle ?? (String.IsNullOrEmpty(_package.Title) ? _package.PackageRegistration.Id : _package.Title); }
            set { _pendingTitle = value; }
        }

        public bool IsCurrent(IPackageVersionModel current)
        {
            return current.Version == Version && current.Id == Id;
        }
    }
}