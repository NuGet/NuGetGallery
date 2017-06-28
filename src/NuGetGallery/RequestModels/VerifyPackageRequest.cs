// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Packaging;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class VerifyPackageRequest
    {
        public VerifyPackageRequest() { }

        public VerifyPackageRequest(PackageMetadata packageMetadata)
        {
            Id = packageMetadata.Id;
            Version = packageMetadata.Version.ToFullStringSafe();
            OriginalVersion = packageMetadata.Version.OriginalVersion;
            LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull();
            Listed = true;
            Language = packageMetadata.Language;
            MinClientVersion = packageMetadata.MinClientVersion;
            FrameworkReferenceGroups = packageMetadata.GetFrameworkReferenceGroups();
            Dependencies = new DependencySetsViewModel(packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());
            DevelopmentDependency = packageMetadata.GetValueFromMetadata("developmentDependency");
            Edit = new EditPackageVersionRequest(packageMetadata);
        }

        public string Id { get; set; }

        /// <summary>
        /// The normalized, full version string (for display purposes).
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The non-normalized, unmodified, original version as defined in the nuspec.
        /// </summary>
        public string OriginalVersion { get; set; }

        public string LicenseUrl { get; set; }
        public bool Listed { get; set; }
        public EditPackageVersionRequest Edit { get; set; }
        public NuGetVersion MinClientVersion { get; set; }
        public string MinClientVersionDisplay { get; set; }
        public string Language { get; set; }
        public string DevelopmentDependency { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public IReadOnlyCollection<FrameworkSpecificGroup> FrameworkReferenceGroups { get; set; }
    }
}