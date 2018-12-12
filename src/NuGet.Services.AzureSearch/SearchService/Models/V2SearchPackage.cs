// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class V2SearchPackage
    {
        public V2SearchPackageRegistration PackageRegistration { get; set; }
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
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Published { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public DateTimeOffset? LastEdited { get; set; }
        public long DownloadCount { get; set; }
        public string FlattenedDependencies { get; set; }

        /// <summary>
        /// Unused by gallery.
        /// </summary>
        public V2SearchDependency[] Dependencies { get; set; }

        /// <summary>
        /// Unused by gallery.
        /// </summary>
        public string[] SupportedFrameworks { get; set; }

        public string MinClientVersion { get; set; }
        public string Hash { get; set; }
        public string HashAlgorithm { get; set; }
        public long PackageFileSize { get; set; }
        public string LicenseUrl { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public object Debug { get; set; }
    }
}
