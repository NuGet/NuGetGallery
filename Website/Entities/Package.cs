﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NuGetGallery
{
    [DisplayColumn("Title")]
    public class Package : IEntity
    {
        public Package()
        {
            Authors = new HashSet<PackageAuthor>();
            Dependencies = new HashSet<PackageDependency>();
            SupportedFrameworks = new HashSet<PackageFramework>();
            Listed = true;
        }

        public PackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }

        public virtual ICollection<PackageStatistics> DownloadStatistics { get; set; }
        public virtual ICollection<PackageAuthor> Authors { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string Copyright { get; set; }

        public DateTime Created { get; set; }
        public virtual ICollection<PackageDependency> Dependencies { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed but *IS* used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string Description { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string ReleaseNotes { get; set; }

        public int DownloadCount { get; set; }

        /// <remarks>
        ///     Is not a property that we support. Maintained for legacy reasons.
        /// </remarks>
        public string ExternalPackageUrl { get; set; }

        [StringLength(10)]
        public string HashAlgorithm { get; set; }

        [StringLength(256)]
        [Required]
        public string Hash { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string IconUrl { get; set; }

        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }
        public DateTime LastUpdated { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string LicenseUrl { get; set; }

        [StringLength(20)]
        public string Language { get; set; }

        public DateTime Published { get; set; }
        public long PackageFileSize { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string ProjectUrl { get; set; }

        public bool RequiresLicenseAcceptance { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        public string Summary { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and *IS* used for searches, but is maintained via Lucene. Db column is nvarchar(max).
        /// </remarks>
        public string Tags { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(64)]
        [Required]
        public string Version { get; set; }

        public bool Listed { get; set; }
        public bool IsPrerelease { get; set; }
        public virtual ICollection<PackageFramework> SupportedFrameworks { get; set; }

        // TODO: it would be nice if we could change the feed so that we don't need to flatten authors and dependencies
        public string FlattenedAuthors { get; set; }
        public string FlattenedDependencies { get; set; }
        public int Key { get; set; }

        [StringLength(44)]
        public string MinClientVersion { get; set; }

        internal string GetCurrentDescription()
        {
            string masterDescription = PackageRegistration != null ? PackageRegistration.Description : null;
            return string.IsNullOrWhiteSpace(masterDescription) ? Description : masterDescription;
        }

        internal string GetCurrentSummary()
        {
            string masterSummary = PackageRegistration != null ? PackageRegistration.Summary : null;
            return string.IsNullOrWhiteSpace(masterSummary) ? Summary : masterSummary;
        }

        internal string GetCurrentIconUrl()
        {
            string masterIconUrl = PackageRegistration != null ? PackageRegistration.IconUrl : null;
            return string.IsNullOrWhiteSpace(masterIconUrl) ? IconUrl : masterIconUrl;
        }

        internal string GetCurrentProjectUrl()
        {
            string masterProjectUrl = PackageRegistration != null ? PackageRegistration.ProjectUrl : null;
            return string.IsNullOrWhiteSpace(masterProjectUrl) ? ProjectUrl : masterProjectUrl;
        }

        internal IEnumerable<string> GetCurrentTags()
        {
            var masterTags = PackageRegistration != null && PackageRegistration.Tags != null ? PackageRegistration.Tags.Select(tag => tag.Name).ToList() : null;
            if (masterTags.AnySafe())
            {
                return masterTags;
            }

            if (Tags == null)
            {
                return Enumerable.Empty<string>();
            }

            return Tags.Trim().Split(' ');
        }

        internal string GetCurrentFlattenedTags()
        {
            var masterFlattenedTags = PackageRegistration != null ? PackageRegistration.FlattenedTags : null;
            if (!String.IsNullOrWhiteSpace(masterFlattenedTags))
            {
                return masterFlattenedTags;
            }

            return Tags;
        }

        internal string GetCurrentTitle(bool allowNull = false)
        {
            // This guy is an exception - version's title beats package registration's title
            string workingTitle = Title;
            if (PackageRegistration != null && string.IsNullOrWhiteSpace(workingTitle))
            {
                workingTitle = PackageRegistration.DefaultTitle;
            }
            
            if (PackageRegistration != null && string.IsNullOrWhiteSpace(workingTitle) && !allowNull)
            {
                workingTitle = PackageRegistration.Id;
            }

            return workingTitle;
        }
    }
}