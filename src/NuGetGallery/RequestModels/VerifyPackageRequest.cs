// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class VerifyPackageRequest
    {
        public VerifyPackageRequest() { }

        public VerifyPackageRequest(PackageMetadata packageMetadata)
        {
            var dependencyGroups = packageMetadata.GetDependencyGroups();

            Id = packageMetadata.Id;
            Version = packageMetadata.Version.ToFullStringSafe();
            OriginalVersion = packageMetadata.Version.OriginalVersion;
            HasSemVer2Version = packageMetadata.Version.IsSemVer2;
            HasSemVer2Dependency = dependencyGroups.Any(d => d.Packages.Any(
                                p => (p.VersionRange.HasUpperBound && p.VersionRange.MaxVersion.IsSemVer2)
                                    || (p.VersionRange.HasLowerBound && p.VersionRange.MinVersion.IsSemVer2)));
            LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull();
            Listed = true;
            Language = packageMetadata.Language;
            MinClientVersionDisplay = packageMetadata.MinClientVersion.ToFullStringSafe();
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
        public bool IsSemVer2 => HasSemVer2Version || HasSemVer2Dependency;
        public bool HasSemVer2Version { get; set; }
        public bool HasSemVer2Dependency { get; set; }
        public string LicenseUrl { get; set; }
        public bool Listed { get; set; }
        public EditPackageVersionRequest Edit { get; set; }
        public string MinClientVersionDisplay { get; set; }
        public string Language { get; set; }
        public string DevelopmentDependency { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public IReadOnlyCollection<FrameworkSpecificGroup> FrameworkReferenceGroups { get; set; }
    }
}