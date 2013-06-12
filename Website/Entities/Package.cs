using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    [DisplayColumn("Title")]
    public class Package : IEntity
    {
        public Package()
        {
            Dependencies = new HashSet<PackageDependency>();
            SupportedFrameworks = new HashSet<PackageFramework>();
            Listed = true;
        }

#pragma warning disable 612
        public PackageDescription GetDescription()
        {
            if (AppliedEdit != null)
            {
                return new PackageDescription
                {
                    Authors = AppliedEdit.Authors,
                    Copyright = AppliedEdit.Copyright,
                    Description = AppliedEdit.Description,
                    Hash = AppliedEdit.Hash,
                    HashAlgorithm = AppliedEdit.HashAlgorithm,
                    IconUrl = AppliedEdit.IconUrl,
                    PackageFileSize = AppliedEdit.PackageFileSize,
                    ProjectUrl = AppliedEdit.ProjectUrl,
                    ReleaseNotes = AppliedEdit.ReleaseNotes,
                    Summary = AppliedEdit.Summary,
                    Tags = AppliedEdit.Tags,
                    Title = AppliedEdit.Title,
                };
            }

            return new PackageDescription
            {
                Authors = this.FlattenedAuthors,
                Copyright = this.Copyright,
                Description = this.Description,
                Hash = this.Hash,
                HashAlgorithm = this.HashAlgorithm,
                IconUrl = this.IconUrl,
                PackageFileSize = this.PackageFileSize,
                ProjectUrl = this.ProjectUrl,
                ReleaseNotes = this.ReleaseNotes,
                Summary = this.Summary,
                Tags = this.Tags,
                Title = this.Title,
            };
        }
#pragma warning restore 612

        public PackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }

        public virtual ICollection<PackageStatistics> DownloadStatistics { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
        public string Copyright { get; set; }

        public DateTime Created { get; set; }
        public virtual ICollection<PackageDependency> Dependencies { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed but *IS* used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
        public string Description { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
        public string ReleaseNotes { get; set; }

        public int DownloadCount { get; set; }

        /// <remarks>
        ///     Is not a property that we support. Maintained for legacy reasons.
        /// </remarks>
        [Obsolete]
        public string ExternalPackageUrl { get; set; }

        [StringLength(10)]
        [Obsolete]
        public string HashAlgorithm { get; set; }

        [StringLength(256)]
        [Required]
        [Obsolete]
        public string Hash { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
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

        [Obsolete]
        public long PackageFileSize { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
        public string ProjectUrl { get; set; }

        public bool RequiresLicenseAcceptance { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
        public string Summary { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and *IS* used for searches, but is maintained via Lucene. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete]
        public string Tags { get; set; }

        [StringLength(256)]
        [Obsolete]
        public string Title { get; set; }

        [StringLength(64)]
        [Required]
        public string Version { get; set; }

        public bool Listed { get; set; }
        public bool IsPrerelease { get; set; }
        public virtual ICollection<PackageFramework> SupportedFrameworks { get; set; }

        [Obsolete]
        public string FlattenedAuthors { get; set; }

        public string FlattenedDependencies { get; set; }
        public int Key { get; set; }

        [StringLength(44)]
        public string MinClientVersion { get; set; }

        public PackageEdit AppliedEdit { get; set; } // This is the currently applied edit, which specifies the actual description of the package. Or NULL.

    }
}
