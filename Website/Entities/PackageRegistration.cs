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
    }
}