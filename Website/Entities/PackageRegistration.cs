using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class PackageRegistration : IEntity
    {
        public PackageRegistration()
        {
            Owners = new HashSet<User>();
            Packages = new HashSet<Package>();
            Tags = new HashSet<Tag>();
        }

        [StringLength(128)]
        [Required]
        public string Id { get; set; }

        public int DownloadCount { get; set; }
        public virtual ICollection<User> Owners { get; set; }
        public virtual ICollection<Package> Packages { get; set; }
        public int Key { get; set; }

        // Sets a default Title to use for package versions that have no Title (instead of the PackageRegistration's ID).
        [StringLength(256)]
        public string DefaultTitle { get; set; }

        // These optional fields override the Package's fields as displayed on the website, if supplied:
        public string Description { get; set; }

        [StringLength(1024)]
        public string Summary { get; set; }

        [StringLength(256)]
        public string IconUrl { get; set; }

        [StringLength(256)]
        public string ProjectUrl { get; set; }

        [StringLength(256)]
        public string SourceCodeUrl { get; set; }

        [StringLength(256)]
        public string IssueTrackerUrl { get; set; }

        // Time the package registration data was modified, but excluding changes to 'ephemeral' data such as DownloadCount.
        public DateTime LastUpdated { get; set; }

        public virtual ICollection<Tag> Tags { get; set; }

        [StringLength(1024)]
        public string FlattenedTags { get; set; }
    }
}