// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Common
{
    public class NuGetPackage
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string NormalizedVersion { get; set; }
        public Uri DownloadUrl { get; set; }
        public string Copyright { get; set; }
        public DateTimeOffset Created { get; set; }
        public string Dependencies { get; set; }
        public string Description { get; set; }
        public int DownloadCount { get; set; }
        public int VersionDownloadCount { get; set; }
        public Uri GalleryDetailsUrl { get; set; }
        public Uri IconUrl { get; set; }
        public bool IsAbsoluteLatestVersion { get; set; }
        public bool IsLatestVersion { get; set; }
        public bool IsPrerelease { get; set; }
        public string Language { get; set; }
        public DateTimeOffset? LastEdited { get; set; }
        public string LicenseNames { get; set; }
        public Uri LicenseReportUrl { get; set; }
        public Uri LicenseUrl { get; set; }
        public string MinClientVersion { get; set; }
        public string PackageHash { get; set; }
        public string PackageHashAlgorithm { get; set; }
        public long PackageSize { get; set; }
        public Uri ProjectUrl { get; set; }
        public DateTimeOffset? Published { get; set; }
        public string ReleaseNotes { get; set; }
        public Uri ReportAbuseUrl { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
    }
}