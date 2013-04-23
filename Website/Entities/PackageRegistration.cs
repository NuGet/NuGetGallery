using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class PackageRegistration : IEntity
    {
        public PackageRegistration()
        {
            Owners = new HashSet<User>();
            Packages = new HashSet<Package>();
        }

        [StringLength(128)]
        [Required]
        public string Id { get; set; }

        public int DownloadCount { get; set; }
        public virtual ICollection<User> Owners { get; set; }
        public virtual ICollection<Package> Packages { get; set; }
        public int Key { get; set; }

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
    }
}