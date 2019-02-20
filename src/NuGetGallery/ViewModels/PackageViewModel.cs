// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        protected readonly Package _package;
        private string _pendingTitle;

        private readonly PackageStatus _packageStatus;
        internal readonly NuGetVersion NuGetVersion;

        public PackageViewModel(Package package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            FullVersion = NuGetVersionFormatter.ToFullString(package.Version);
            IsSemVer2 = package.SemVerLevelKey == SemVerLevelKey.SemVer2;

            Version = String.IsNullOrEmpty(package.NormalizedVersion) ?
                NuGetVersionFormatter.Normalize(package.Version) :
                package.NormalizedVersion;

            NuGetVersion = NuGetVersion.Parse(FullVersion);

            Description = package.Description;
            ReleaseNotes = package.ReleaseNotes;
            IconUrl = package.IconUrl;
            LatestVersion = package.IsLatest;
            LatestVersionSemVer2 = package.IsLatestSemVer2;
            LatestStableVersion = package.IsLatestStable;
            LatestStableVersionSemVer2 = package.IsLatestStableSemVer2;
            LastUpdated = package.Published;
            Listed = package.Listed;
            _packageStatus = package.PackageStatusKey;
            DownloadCount = package.DownloadCount;
            Prerelease = package.IsPrerelease;
        }

        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string IconUrl { get; set; }
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
        public string FullVersion { get; }
        public bool IsSemVer2 { get; }

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