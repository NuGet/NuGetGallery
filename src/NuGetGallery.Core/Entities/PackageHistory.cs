using System;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// This records the OLD metadata of a particular package, before an edit was applied.
    /// </summary>
    public class PackageHistory : IEntity
    {
        public int Key { get; set; }

        public Package Package { get; set; }
        public int PackageKey { get; set; }

        /// <summary>
        /// The user who generated this old metadata. NULL if unknown.
        /// </summary>
        public User User { get; set; }
        public int? UserKey { get; set; }

        /// <summary>
        /// Time the metadata replacement occurred
        /// </summary
        public DateTime Timestamp { get; set; }

        //////////////// The rest are same as on Package ////////////

        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string Hash { get; set; }
        [StringLength(10)]
        public string HashAlgorithm { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public long PackageFileSize { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime Published { get; set; }
    }
}