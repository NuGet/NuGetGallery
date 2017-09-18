﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class EditPackageVersionRequest
    {
        public const string TitleStr = "Title";
        public const string IconUrlStr = "Icon URL";
        public const string DescriptionStr = "Description";
        public const string SummaryStr = "Summary (shown in package search results)";
        public const string ProjectUrlStr = "Project Home Page URL";
        public const string AuthorsStr = "Authors (comma separated  - e.g. 'Anna, Bob, Carl')";
        public const string CopyrightStr = "Copyright";
        public const string TagsStr = "Tags (space separated - e.g. 'ASP.NET Templates MVC')";
        public const string ReleaseNotesStr = "Release Notes (for this version)";
        public const string RequiresLicenseAcceptanceStr = "Requires license acceptance";

        public EditPackageVersionRequest()
        {
        }

        public EditPackageVersionRequest(PackageMetadata packageMetadata)
        {

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
            VersionTitle = packageMetadata.Title;

            ReadMe = new ReadMeRequest();
        }

        public EditPackageVersionRequest(Package package, PackageEdit pendingMetadata)
        {
            var metadata = pendingMetadata ?? new PackageEdit
            {
                Authors = package.FlattenedAuthors,
                Copyright = package.Copyright,
                Description = package.Description,
                IconUrl = package.IconUrl,
                LicenseUrl = package.LicenseUrl,
                ProjectUrl = package.ProjectUrl,
                ReleaseNotes = package.ReleaseNotes,
                RequiresLicenseAcceptance = package.RequiresLicenseAcceptance,
                Summary = package.Summary,
                Tags = package.Tags,
                Title = package.Title,
            };
            Authors = metadata.Authors;
            Copyright = metadata.Copyright;
            Description = metadata.Description;
            IconUrl = metadata.IconUrl;
            LicenseUrl = metadata.LicenseUrl;
            ProjectUrl = metadata.ProjectUrl;
            ReleaseNotes = metadata.ReleaseNotes;
            RequiresLicenseAcceptance = metadata.RequiresLicenseAcceptance;
            Summary = metadata.Summary;
            Tags = metadata.Tags;
            VersionTitle = metadata.Title;
            ReadMeState = metadata.ReadMeState;

            ReadMe = new ReadMeRequest();
        }

        // We won't show this in the UI, and we won't actually honor edits to it at the moment, by our current policy.
        public string LicenseUrl { get; set; }

        [StringLength(256)]
        [Display(Name = TitleStr)]
        [DataType(DataType.Text)]
        public string VersionTitle { get; set; } // not naming it Title in our model because that would clash with page Title in the property bag. Blech.

        [StringLength(256)]
        [Display(Name = IconUrlStr)]
        [DataType(DataType.ImageUrl)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = Constants.UrlValidationErrorMessage)]
        public string IconUrl { get; set; }

        [StringLength(1024)]
        [DataType(DataType.MultilineText)]
        [Display(Name = SummaryStr)]
        public string Summary { get; set; }

        [StringLength(4000)]
        [DataType(DataType.MultilineText)]
        [Display(Name = DescriptionStr)]
        [Required]
        public string Description { get; set; }

        [StringLength(256)]
        [Display(Name = ProjectUrlStr)]
        [DataType(DataType.Text)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = Constants.UrlValidationErrorMessage)]
        public string ProjectUrl { get; set; }

        [StringLength(512)]
        [Display(Name = AuthorsStr)]
        [DataType(DataType.Text)]
        [Required]
        public string Authors { get; set; }

        [StringLength(512)]
        [Display(Name = CopyrightStr)]
        [DataType(DataType.Text)]
        public string Copyright { get; set; }

        [StringLength(1024)]
        [DataType(DataType.Text)]
        [Display(Name = TagsStr)]
        public string Tags { get; set; }

        [Display(Name = ReleaseNotesStr)]
        [DataType(DataType.MultilineText)]
        public string ReleaseNotes { get; set; }

        [Display(Name = RequiresLicenseAcceptanceStr)]
        public bool RequiresLicenseAcceptance { get; set; }

        public PackageEditReadMeState ReadMeState { get; set; }

        public ReadMeRequest ReadMe { get; set; }

        /// <summary>
        /// Applied the edit to a package
        /// </summary>
        /// <param name="package">Package to apply edits to</param>
        /// <returns>Edited package</returns>
        public Package ApplyTo(Package package)
        {
            package.FlattenedAuthors = Authors;
            package.Copyright = Copyright;
            package.Description = Description;
            package.IconUrl = IconUrl;
            package.LicenseUrl = LicenseUrl;
            package.ProjectUrl = ProjectUrl;
            package.ReleaseNotes = ReleaseNotes;
            package.RequiresLicenseAcceptance = RequiresLicenseAcceptance;
            package.Summary = Summary;
            package.Tags = Tags;
            package.Title = VersionTitle;

            return package;
        }
    }
}
