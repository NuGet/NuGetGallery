// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class PackageViewModel : IPackageVersionModel
    {
        public string Description { get; set; }
        public string ReleaseNotes { get; set; }
        public string IconUrl { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool LatestVersionSemVer2 { get; set; }
        public bool LatestStableVersionSemVer2 { get; set; }
        public bool DevelopmentDependency { get; set; }
        public bool Prerelease { get; set; }
        public int DownloadCount { get; set; }
        public bool Listed { get; set; }
        public bool FailedValidation { get; set; }
        public bool Available { get; set; }
        public bool Validating { get; set; }
        public bool Deleted { get; set; }
        public int TotalDownloadCount { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public string FullVersion { get; set; }
        public PackageStatusSummary PackageStatusSummary { get; set; }

        public bool IsCurrent(IPackageVersionModel current)
        {
            return current.Version == Version && current.Id == Id;
        }
    }
}