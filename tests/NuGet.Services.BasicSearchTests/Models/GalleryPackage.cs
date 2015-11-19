// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.BasicSearchTests.Models
{
    public class GalleryPackage
    {
        public PackageRegistration PackageRegistration { get; set; }

        public string Version { get; set; }

        public string NormalizedVersion { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string Authors { get; set; }

        public string Copyright { get; set; }

        public string Language { get; set; }

        public string Tags { get; set; }

        public string ReleaseNotes { get; set; }

        public string ProjectUrl { get; set; }

        public string IconUrl { get; set; }

        public bool IsLatestStable { get; set; }

        public bool IsLatest { get; set; }

        public bool Listed { get; set; }

        public string Created { get; set; }

        public string Published { get; set; }

        public string LastUpdated { get; set; }

        public string LastEdited { get; set; }

        public int DownloadCount { get; set; }

        public string FlattenedDependencies { get; set; }

        public string MinClientVersion { get; set; }

        public string Hash { get; set; }

        public string HashAlgorithm { get; set; }

        public int PackageFileSize { get; set; }

        public string LicenseUrl { get; set; }

        public bool RequiresLicenseAcceptance { get; set; }

        public string LicenseNames { get; set; }

        public string LicenseReportUrl { get; set; }

        public bool HideLicenseReport { get; set; }
    }
}