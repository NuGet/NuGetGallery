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

        public PackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }

#pragma warning disable 612 // TODO: PackageMetadata DB contraction
        private PackageMetadata _metadata;
        public PackageMetadata Metadata
        {
            get
            {
                // TODO: PackageMetadata DB contraction - this null check workaround code will become unnecessary
                if (_metadata == null)
                {
                    _metadata = new PackageMetadata
                    {
                        Authors = this.FlattenedAuthors,
                        Copyright = this.Copyright,
                        Description = this.Description,
                        EditName = "OriginalMetadata",
                        User = null,
                        Hash = this.Hash,
                        HashAlgorithm = this.HashAlgorithm,
                        IconUrl = this.IconUrl,
                        IsCompleted = true,
                        LicenseUrl = this.LicenseUrl,
                        Package = this,
                        PackageKey = this.Key,
                        PackageFileSize = this.PackageFileSize,
                        ProjectUrl = this.ProjectUrl,
                        ReleaseNotes = this.ReleaseNotes,
                        Summary = this.Summary,
                        Tags = this.Tags,
                        Timestamp = DateTime.UtcNow,
                        Title = this.Title,
                        TriedCount = 0,
                    };
                }

                return _metadata;
            }
            set
            {
                _metadata = value;
            }
        }
#pragma warning restore 612

        public int? MetadataKey { get; set; }

        public virtual ICollection<PackageStatistics> DownloadStatistics { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string Copyright { get; set; }

        public DateTime Created { get; set; }
        public virtual ICollection<PackageDependency> Dependencies { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed but *IS* used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string Description { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string ReleaseNotes { get; set; }

        public int DownloadCount { get; set; }

        /// <remarks>
        ///     Is not a property that we support. Maintained for legacy reasons.
        /// </remarks>
        [Obsolete]
        public string ExternalPackageUrl { get; set; }

        [StringLength(10)]
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string HashAlgorithm { get; set; }

        [StringLength(256)]
        [Required]
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string Hash { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string IconUrl { get; set; }

        public bool IsLatest { get; set; }
        public bool IsLatestStable { get; set; }
        public DateTime LastUpdated { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string LicenseUrl { get; set; }

        [StringLength(20)]
        public string Language { get; set; }

        public DateTime Published { get; set; }

        [Obsolete] // TODO: PackageMetadata DB contraction
        public long PackageFileSize { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string ProjectUrl { get; set; }

        public bool RequiresLicenseAcceptance { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and not used for searches. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string Summary { get; set; }

        /// <remarks>
        ///     Has a max length of 4000. Is not indexed and *IS* used for searches, but is maintained via Lucene. Db column is nvarchar(max).
        /// </remarks>
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string Tags { get; set; }

        [StringLength(256)]
        [Obsolete] // TODO: PackageMetadata DB contraction
        public string Title { get; set; }

        [StringLength(64)]
        [Required]
        public string Version { get; set; }

        public bool Listed { get; set; }
        public bool IsPrerelease { get; set; }
        public virtual ICollection<PackageFramework> SupportedFrameworks { get; set; }

        [Obsolete] // TODO: PackageMetadata DB contraction
        public string FlattenedAuthors { get; set; }

        public string FlattenedDependencies { get; set; }
        public int Key { get; set; }

        [StringLength(44)]
        public string MinClientVersion { get; set; }
    }
}
