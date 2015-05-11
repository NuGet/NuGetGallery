// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// This records the OLD metadata of a particular package, before an edit was applied.
    /// </summary>
    public class PackageHistory : IEntity
    {
        public PackageHistory()
        {
        }

        public PackageHistory(Package package)
        {
            this.Package = package;
            this.User = package.User;
            this.Timestamp = DateTime.UtcNow;
            this.Title = package.Title;
            this.Authors = package.FlattenedAuthors;
            this.Copyright = package.Copyright;
            this.Description = package.Description;
            this.IconUrl = package.IconUrl;
            this.LicenseUrl = package.LicenseUrl;
            this.ProjectUrl = package.ProjectUrl;
            this.ReleaseNotes = package.ReleaseNotes;
            this.RequiresLicenseAcceptance = package.RequiresLicenseAcceptance;
            this.Summary = package.Summary;
            this.Tags = package.Tags;
            this.Hash = package.Hash;
            this.HashAlgorithm = package.HashAlgorithm;
            this.PackageFileSize = package.PackageFileSize;
            this.LastUpdated = package.LastUpdated;
            this.Published = package.Published;
        }

        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        /// <summary>
        /// The user who generated this old metadata. NULL if unknown.
        /// </summary>
        public User User { get; set; }
        public int? UserKey { get; set; }

        /// <summary>
        /// Time the metadata replacement occurred (UTC)
        /// </summary
        public DateTime Timestamp { get; set; }

        //////////////// The rest are same as on Package ////////////

        [StringLength(256)]
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }

        [StringLength(256)]
        public string Hash { get; set; }

        [StringLength(10)]
        public string HashAlgorithm { get; set; }

        public long PackageFileSize { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime Published { get; set; }
    }
}