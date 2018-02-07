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

        public VerifyPackageRequest(PackageMetadata packageMetadata, IEnumerable<User> possibleOwners, PackageRegistration existingPackageRegistration)
        {
            Id = packageMetadata.Id;
            Version = packageMetadata.Version.ToFullStringSafe();
            OriginalVersion = packageMetadata.Version.OriginalVersion;
            HasSemVer2Version = packageMetadata.Version.IsSemVer2;
            HasSemVer2Dependency = packageMetadata.GetDependencyGroups().Any(d => d.Packages.Any(
                                p => (p.VersionRange.HasUpperBound && p.VersionRange.MaxVersion.IsSemVer2)
                                    || (p.VersionRange.HasLowerBound && p.VersionRange.MinVersion.IsSemVer2)));
            
            // Verifiable fields
            Language = packageMetadata.Language;
            MinClientVersionDisplay = packageMetadata.MinClientVersion.ToFullStringSafe();
            FrameworkReferenceGroups = packageMetadata.GetFrameworkReferenceGroups();
            Dependencies = new DependencySetsViewModel(packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());
            DevelopmentDependency = packageMetadata.GetValueFromMetadata("developmentDependency");
            Authors = packageMetadata.Authors.Flatten();
            Copyright = packageMetadata.Copyright;
            Description = packageMetadata.Description;
            IconUrl = packageMetadata.IconUrl.ToEncodedUrlStringOrNull();
            LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull();
            ProjectUrl = packageMetadata.ProjectUrl.ToEncodedUrlStringOrNull();
            ReleaseNotes = packageMetadata.ReleaseNotes;
            RequiresLicenseAcceptance = packageMetadata.RequireLicenseAcceptance;
            Summary = packageMetadata.Summary;
            Tags = PackageHelper.ParseTags(packageMetadata.Tags);
            Title = packageMetadata.Title;
            IsNewId = existingPackageRegistration == null;
            if (!IsNewId)
            {
                ExistingOwners = string.Join(", ", ParseUserList(existingPackageRegistration.Owners));
            }

            // Editable server-state
            Listed = true;
            Edit = new EditPackageVersionReadMeRequest();
            PossibleOwners = ParseUserList(possibleOwners);
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

        /// <summary>
        /// This is a new ID.
        /// There are no existing packages with this ID.
        /// </summary>
        public bool IsNewId { get; set; }

        /// <summary>
        /// The username of the <see cref="User"/> to upload the package as.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// The <see cref="User"/>s that own the existing package registration that this package will be added to in a string.
        /// E.g. "alice, bob, chad".
        /// </summary>
        public string ExistingOwners { get; set; }

        /// <summary>
        /// The usernames of the <see cref="User"/>s that the current user can upload the package as.
        /// </summary>
        public IReadOnlyCollection<string> PossibleOwners { get; set; }

        public bool IsSemVer2 => HasSemVer2Version || HasSemVer2Dependency;
        public bool HasSemVer2Version { get; set; }
        public bool HasSemVer2Dependency { get; set; }

        // Editable server-state
        public bool Listed { get; set; }
        public EditPackageVersionReadMeRequest Edit { get; set; }

        // Verifiable fields
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public string DevelopmentDependency { get; set; }
        public IReadOnlyCollection<FrameworkSpecificGroup> FrameworkReferenceGroups { get; set; }
        public string IconUrl { get; set; }
        public string Language { get; set; }
        public string LicenseUrl { get; set; }
        public string MinClientVersionDisplay { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }

        private static IReadOnlyCollection<string> ParseUserList(IEnumerable<User> users)
        {
            return users.Select(u => u.Username).ToList();
        }
    }
}