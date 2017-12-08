// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        private readonly Package _package;
        private readonly bool _isSemVer2;
        private string _pendingTitle;
        private string _fullVersion;

        private readonly PackageStatus _packageStatus;
        internal readonly NuGetVersion NuGetVersion;

        public PackageViewModel(Package package)
        {
            _package = package;

            _fullVersion = NuGetVersionFormatter.ToFullStringOrFallback(package.Version, fallback: package.Version);
            _isSemVer2 = package.SemVerLevelKey == SemVerLevelKey.SemVer2;

            Version = String.IsNullOrEmpty(package.NormalizedVersion) ?
                NuGetVersionFormatter.Normalize(package.Version) :
                package.NormalizedVersion;

            NuGetVersion = NuGetVersion.Parse(_fullVersion);

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
            _packageStatus = package.PackageStatusKey;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;
            LicenseReportUrl = package.LicenseReportUrl;

            var licenseNames = package.LicenseNames;
            if (!string.IsNullOrEmpty(licenseNames))
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
        public bool FailedValidation => _packageStatus == PackageStatus.FailedValidation;
        public bool Available => _packageStatus == PackageStatus.Available;
        public bool Validating => _packageStatus == PackageStatus.Validating;
        public bool Deleted => _packageStatus == PackageStatus.Deleted;

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

        public PackageStatusSummary PackageStatusSummary
        {
            get
            {
                switch (_packageStatus)
                {
                    case PackageStatus.Validating:
                    {
                        return PackageStatusSummary.Validating;
                    }
                    case PackageStatus.FailedValidation:
                    {
                        return PackageStatusSummary.FailedValidation;
                    }
                    case PackageStatus.Available:
                    {
                        return Listed ? PackageStatusSummary.Listed : PackageStatusSummary.Unlisted;
                    }
                    case PackageStatus.Deleted:
                    {
                        return PackageStatusSummary.None;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(PackageStatus));
                }
            }
        }
    }
}